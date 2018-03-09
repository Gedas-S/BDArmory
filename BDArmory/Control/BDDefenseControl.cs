using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.Radar;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Control
{
    public class BDDefenseControl : PartModule, IBDWMModule
    {
        public MissileFire WeaponManager;
        public MissileFire GuardMode;
        
        #region CounterMeasure
        public bool isChaffing;
        public bool isFlaring;
        public bool isECMJamming;

        bool isLegacyCMing;

        int cmCounter;
        int cmAmount = 5;

        public bool underAttack;
        public bool underFire;
        Coroutine ufRoutine;

        //missile warning
        public bool missileIsIncoming;
        public float incomingMissileDistance = float.MaxValue;
        public Vessel incomingMissileVessel;

        public Vector3 incomingThreatPosition;
        public Vessel incomingThreatVessel;

        //threat scanning
        float guardViewScanDirection = 1;
        float guardViewScanRate = 200;
        float currentGuardViewAngle;
        private Transform vrt;

        Vector3 debugGuardViewDirection;
        bool focusingOnTarget;
        float focusingOnTargetTimer;

        public Transform viewReferenceTransform
        {
            get
            {
                if (vrt == null)
                {
                    vrt = (new GameObject()).transform;
                    vrt.parent = transform;
                    vrt.localPosition = Vector3.zero;
                    vrt.rotation = Quaternion.LookRotation(-transform.forward, -vessel.ReferenceTransform.forward);
                }

                return vrt;
            }
        }

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Automatic defense"),
            UI_Toggle()]
        public bool autoMode;
        public bool Enabled => autoMode || GuardMode.guardMode;
        public string Name { get; protected set; } = "Automatic Defense";
        public void Toggle()
        {
            autoMode = !autoMode;
        }

        void Start()
        {
            StartCoroutine(MissileWarningResetRoutine());
        }

        void FixedUpdate()
        {
            if (Enabled)
            {
                if (missileIsIncoming && !isLegacyCMing)
                {
                    StartCoroutine(LegacyCMRoutine());
                }
            }
        }

        void OnGUI()
        {
            if (HighLogic.LoadedSceneIsFlight && vessel == FlightGlobals.ActiveVessel &&
                BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled
                && BDArmorySettings.DRAW_DEBUG_LINES)
            {
                if (Enabled)
                {
                    BDGUIUtils.DrawLineBetweenWorldPositions(part.transform.position,
                        part.transform.position + (debugGuardViewDirection * 25), 2, Color.yellow);
                }

                if (incomingMissileVessel)
                {
                    BDGUIUtils.DrawLineBetweenWorldPositions(part.transform.position,
                        incomingMissileVessel.transform.position, 5, Color.cyan);
                }
            }
        }

        void ScanForThreats()
        {
            float finalMaxAngle = GuardMode.guardAngle / 2;
            float finalScanDirectionAngle = currentGuardViewAngle;
            if (GuardMode.currentTarget?.Vessel != null)
            {
                if (focusingOnTarget)
                {
                    if (focusingOnTargetTimer > 3)
                    {
                        focusingOnTargetTimer = 0;
                        focusingOnTarget = false;
                    }
                    else
                    {
                        focusingOnTargetTimer += Time.fixedDeltaTime;
                    }
                    finalMaxAngle = 20;
                    finalScanDirectionAngle =
                        VectorUtils.SignedAngle(viewReferenceTransform.forward,
                            GuardMode.currentTarget.Vessel.transform.position - viewReferenceTransform.position,
                            viewReferenceTransform.right) + currentGuardViewAngle;
                }
                else
                {
                    if (focusingOnTargetTimer > 2)
                    {
                        focusingOnTargetTimer = 0;
                        focusingOnTarget = true;
                    }
                    else
                    {
                        focusingOnTargetTimer += Time.fixedDeltaTime;
                    }
                }
            }


            float angleDelta = guardViewScanRate * Time.fixedDeltaTime;
            ViewScanResults results;
            debugGuardViewDirection = RadarUtils.GuardScanInDirection(WeaponManager, finalScanDirectionAngle,
                viewReferenceTransform, angleDelta, out results, GuardMode.guardRange);

            currentGuardViewAngle += guardViewScanDirection * angleDelta;
            if (Mathf.Abs(currentGuardViewAngle) > finalMaxAngle)
            {
                currentGuardViewAngle = Mathf.Sign(currentGuardViewAngle) * finalMaxAngle;
                guardViewScanDirection = -guardViewScanDirection;
            }

            if (results.foundMissile)
            {
                if (WeaponManager.rwr && !WeaponManager.rwr.rwrEnabled)
                {
                    WeaponManager.rwr.EnableRWR();
                }
            }

            if (results.foundHeatMissile)
            {
                StartCoroutine(UnderAttackRoutine());

                if (!isFlaring)
                {
                    StartCoroutine(FlareRoutine(2.5f));
                    StartCoroutine(ResetMissileThreatDistanceRoutine());
                }
                incomingThreatPosition = results.threatPosition;

                if (results.threatVessel)
                {
                    if (!incomingMissileVessel ||
                        (incomingMissileVessel.transform.position - vessel.transform.position).sqrMagnitude >
                        (results.threatVessel.transform.position - vessel.transform.position).sqrMagnitude)
                    {
                        incomingMissileVessel = results.threatVessel;
                    }
                }
            }

            if (results.foundRadarMissile)
            {
                StartCoroutine(UnderAttackRoutine());

                FireChaff();
                FireECM();

                incomingThreatPosition = results.threatPosition;

                if (results.threatVessel)
                {
                    if (!incomingMissileVessel ||
                        (incomingMissileVessel.transform.position - vessel.transform.position).sqrMagnitude >
                        (results.threatVessel.transform.position - vessel.transform.position).sqrMagnitude)
                    {
                        incomingMissileVessel = results.threatVessel;
                    }
                }
            }

            if (results.foundAGM)
            {
                StartCoroutine(UnderAttackRoutine());

                //do smoke CM here.
            }

            incomingMissileDistance = Mathf.Min(results.missileThreatDistance, incomingMissileDistance);

            if (results.firingAtMe)
            {
                StartCoroutine(UnderAttackRoutine());

                incomingThreatPosition = results.threatPosition;
                if (ufRoutine != null)
                {
                    StopCoroutine(ufRoutine);
                    underFire = false;
                }
                if (results.threatWeaponManager != null)
                {
                    TargetInfo nearbyFriendly = BDATargetManager.GetClosestFriendly(WeaponManager);
                    TargetInfo nearbyThreat = BDATargetManager.GetTargetFromWeaponManager(results.threatWeaponManager);

                    if (nearbyThreat?.weaponManager != null && nearbyFriendly?.weaponManager != null)
                        if (nearbyThreat.weaponManager.team != WeaponManager.team &&
                            nearbyFriendly.weaponManager.team == WeaponManager.team)
                        //turns out that there's no check for AI on the same team going after each other due to this.  Who knew?
                        {
                            if (nearbyThreat == GuardMode.currentTarget && nearbyFriendly.weaponManager.currentTarget != null)
                            //if being attacked by the current target, switch to the target that the nearby friendly was engaging instead
                            {
                                GuardMode.SetOverrideTarget(nearbyFriendly.weaponManager.currentTarget);
                                nearbyFriendly.weaponManager.SetOverrideTarget(nearbyThreat);
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                    Debug.Log("[BDArmory]: " + vessel.vesselName + " called for help from " +
                                              nearbyFriendly.Vessel.vesselName + " and took its target in return");
                                //basically, swap targets to cover each other
                            }
                            else
                            {
                                //otherwise, continue engaging the current target for now
                                nearbyFriendly.weaponManager.SetOverrideTarget(nearbyThreat);
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                    Debug.Log("[BDArmory]: " + vessel.vesselName + " called for help from " +
                                              nearbyFriendly.Vessel.vesselName);
                            }
                        }
                }
                ufRoutine = StartCoroutine(UnderFireRoutine());
            }
        }

        public void ForceWideViewScan()
        {
            focusingOnTarget = false;
            focusingOnTargetTimer = 1;
        }

        public void ReceiveLaunchWarning(Vector3 source, Vector3 direction)
        {
            incomingThreatPosition = source;

            if (Enabled)
            {
                FireAllCountermeasures(UnityEngine.Random.Range(2, 4));
            }
        }

        public void RWRWarning(Vector3 source, RadarWarningReceiver.RWRThreatTypes type)
        {
            if (Enabled)
            {
                FireChaff();
                // TODO: if torpedo inbound, also fire accoustic decoys (not yet implemented...)
            }
        }

        public void IncomingMissileWarning(MissileBase missile)
        {
            if (!isFlaring && missile.TargetingMode == MissileBase.TargetingModes.Heat && Vector3.Angle(missile.GetForwardTransform(), transform.position - missile.transform.position) < 20)
            {
                StartCoroutine(FlareRoutine(GuardMode.targetScanInterval * 0.75f));
            }
        }

        public void FireAllCountermeasures(int count)
        {
            StartCoroutine(AllCMRoutine(count));
        }

        public void FireECM()
        {
            if (!isECMJamming)
            {
                StartCoroutine(ECMRoutine());
            }
        }

        public void FireChaff()
        {
            if (!isChaffing)
            {
                StartCoroutine(ChaffRoutine());
            }
        }

        IEnumerator MissileWarningResetRoutine()
        {
            while (enabled)
            {
                missileIsIncoming = false;
                yield return new WaitForSeconds(1);
            }
        }

        IEnumerator ResetMissileThreatDistanceRoutine()
        {
            yield return new WaitForSeconds(8);
            incomingMissileDistance = float.MaxValue;
        }

        IEnumerator UnderFireRoutine()
        {
            underFire = true;
            yield return new WaitForSeconds(3);
            underFire = false;
        }

        IEnumerator UnderAttackRoutine()
        {
            underAttack = true;
            yield return new WaitForSeconds(3);
            underAttack = false;
        }

        IEnumerator ECMRoutine()
        {
            isECMJamming = true;
            //yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 1f));
            List<ModuleECMJammer>.Enumerator ecm = vessel.FindPartModulesImplementing<ModuleECMJammer>().GetEnumerator();
            while (ecm.MoveNext())
            {
                if (ecm.Current == null) continue;
                if (ecm.Current.jammerEnabled) yield break;
                ecm.Current.EnableJammer();
            }
            ecm.Dispose();
            yield return new WaitForSeconds(10.0f);
            isECMJamming = false;

            List<ModuleECMJammer>.Enumerator ecm1 = vessel.FindPartModulesImplementing<ModuleECMJammer>().GetEnumerator();
            while (ecm1.MoveNext())
            {
                if (ecm1.Current == null) continue;
                ecm1.Current.DisableJammer();
            }
            ecm1.Dispose();
        }

        IEnumerator ChaffRoutine()
        {
            isChaffing = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 1f));
            List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
            while (cm.MoveNext())
            {
                if (cm.Current == null) continue;
                if (cm.Current.cmType == CMDropper.CountermeasureTypes.Chaff)
                {
                    cm.Current.DropCM();
                }
            }
            cm.Dispose();

            yield return new WaitForSeconds(0.6f);

            isChaffing = false;
        }

        IEnumerator FlareRoutine(float time)
        {
            if (isFlaring) yield break;
            time = Mathf.Clamp(time, 2, 8);
            isFlaring = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 1f));
            float flareStartTime = Time.time;
            while (Time.time - flareStartTime < time)
            {
                List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
                while (cm.MoveNext())
                {
                    if (cm.Current == null) continue;
                    if (cm.Current.cmType == CMDropper.CountermeasureTypes.Flare)
                    {
                        cm.Current.DropCM();
                    }
                }
                cm.Dispose();
                yield return new WaitForSeconds(0.6f);
            }
            isFlaring = false;
        }

        IEnumerator AllCMRoutine(int count)
        {
            for (int i = 0; i < count; i++)
            {
                List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
                while (cm.MoveNext())
                {
                    if (cm.Current == null) continue;
                    if ((cm.Current.cmType == CMDropper.CountermeasureTypes.Flare && !isFlaring)
                        || (cm.Current.cmType == CMDropper.CountermeasureTypes.Chaff && !isChaffing)
                        || (cm.Current.cmType == CMDropper.CountermeasureTypes.Smoke))
                    {
                        cm.Current.DropCM();
                    }
                }
                cm.Dispose();
                isFlaring = true;
                isChaffing = true;
                yield return new WaitForSeconds(1f);
            }
            isFlaring = false;
            isChaffing = false;
        }

        IEnumerator LegacyCMRoutine()
        {
            isLegacyCMing = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(.2f, 1f));
            if (incomingMissileDistance < 2500)
            {
                cmAmount = Mathf.RoundToInt((2500 - incomingMissileDistance) / 400);
                List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
                while (cm.MoveNext())
                {
                    if (cm.Current == null) continue;
                    cm.Current.DropCM();
                }
                cm.Dispose();
                cmCounter++;
                if (cmCounter < cmAmount)
                {
                    yield return new WaitForSeconds(0.15f);
                }
                else
                {
                    cmCounter = 0;
                    yield return new WaitForSeconds(UnityEngine.Random.Range(.5f, 1f));
                }
            }
            isLegacyCMing = false;
        }

        #endregion
    }
}
