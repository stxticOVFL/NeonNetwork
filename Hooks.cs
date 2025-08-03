using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace NeonNetwork
{
    [HarmonyPatch]
    public class Hooks
    {        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MainMenu), "SetState")]
        public static void OnComplete(MainMenu.State newState)
        {
            if (!NeonNetwork.initialized && newState == MainMenu.State.Title) 
                NeonNetwork.instance.Initialize();
        }
    }
}
