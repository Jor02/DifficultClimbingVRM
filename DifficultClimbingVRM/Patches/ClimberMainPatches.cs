using System;
using HarmonyLib;
using UnityEngine;

namespace DifficultClimbingVRM.Patches
{
    internal static class ClimberMainPatches
    {
        public static event Action<GameObject> HatSpawned = null;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ClimberMain), "SpawnHat")]
        static void SpawnHat(GameObject ___hat)
        {
            HatSpawned?.Invoke(___hat);
        }
    }
}
