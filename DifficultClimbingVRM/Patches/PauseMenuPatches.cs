using System;
using HarmonyLib;

namespace DifficultClimbingVRM.Patches
{
    internal static class PauseMenuPatches
    {
        public static PauseMenu PauseMenu { get; set; }
        public static event Action PauseMenuOpened = null;
        public static event Action PauseMenuClosed = null;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PauseMenu), "ResumeGame")]
        static void ResumeGamePatch(PauseMenu __instance)
        {
            PauseMenu = __instance;
            PauseMenuClosed?.Invoke();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PauseMenu), "PauseGame")]
        static void PauseGamePatch(PauseMenu __instance)
        {
            PauseMenu = __instance;
            PauseMenuOpened?.Invoke();
        }
    }
}
