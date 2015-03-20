//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18449
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using UnityEngine;

namespace BahaTurret
{
	public class BDInputUtils
	{
		public static string GetInputString()
		{
			
			//keyCodes
			int numberOfKeycodes = System.Enum.GetNames(typeof(KeyCode)).Length;
			for(int i = 0; i < numberOfKeycodes; i++)
			{
				if(Input.GetKey((KeyCode)i))
				{
					string output = ((KeyCode)i).ToString();
					
					if(output.Contains("Keypad"))
					{
						output = "["+output.Substring(6).ToLower()+"]";
					}
					else if(output.Contains("Alpha"))
					{
						output = output.Substring(5);
					}
					else //lower case key
					{
						output = output.ToLower();
					}
					
					//modifiers
					if(output.Contains("control"))
					{
						output = output.Split('c')[0] + " ctrl";
					}
					else if(output.Contains("alt"))
					{
						output = output.Split('a')[0] + " alt";
					}
					else if(output.Contains("shift"))
					{
						output = output.Split ('s')[0] + " shift";
					}
					else if(output.Contains("command"))
					{
						output = output.Split('c')[0]+" cmd";
					}
					
					
					//special keys
					else if(output == "backslash")
					{
						output = @"\";
					}
					else if(output == "backquote")
					{
						output = "`";
					}
					else if(output == "[period]")
					{
						output = "[.]";
					}
					else if(output == "[plus]")
					{
						output = "[+]";
					}
					else if(output == "[multiply]")
					{
						output = "[*]";
					}
					else if(output == "[divide]")
					{
						output = "[/]";
					}
					else if(output == "[minus]")
					{
						output = "[-]";
					}
					else if(output == "[enter]")
					{
						output = "enter";
					}
					else if(output.Contains("page"))
					{
						output = output.Insert(4, " ");
					}
					else if(output.Contains("arrow"))
					{
						output = output.Split('a')[0];
					}
					else if(output == "capslock")
					{
						output = "caps lock";
					}
					else if(output == "minus")
					{
						output = "-";
					}
					
					
					//test if input is valid
					try
					{
						if(Input.GetKey(output))
						{
							return output;
						}
					}
					catch(System.ArgumentException)
					{
						Debug.Log ("Invalid key string: "+output);
						return string.Empty;
					}
				}
			}

			//mouse
			for(int m = 0; m < 6; m++)
			{
				string inputString = "mouse "+m;
				try
				{
					if(Input.GetKey(inputString))
					{
						return inputString;
					}
				}
				catch(UnityException)
				{
					Debug.Log ("Invalid mouse: "+inputString);
				}
			}
			
			//joysticks
			for(int j = 1; j < 12; j++)
			{
				for(int b = 0; b<20; b++)
				{
					string inputString = "joystick "+j+" button "+b;
					try
					{
						if(Input.GetKey(inputString))
						{
							return inputString;
						}
					}
					catch(UnityException)
					{
						return string.Empty;
					}
				}
			}
			
			return string.Empty;
		}
	}
}

