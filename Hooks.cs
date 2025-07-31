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
        public static void OnComplete(ref MainMenu __instance, ref MainMenu.State newState)
        {
            if (!NeonNetwork.initialized && newState == MainMenu.State.Title) 
                NeonNetwork.instance.Initialize();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LevelPlaythrough), "Update")]
        [HarmonyPriority(Priority.First)]
        public static void RemoveCap(ref long maxLevelTime) => maxLevelTime = long.MaxValue;
    }
}
