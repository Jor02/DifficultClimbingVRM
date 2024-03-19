using System;
using HarmonyLib;
using UnityEngine;

namespace DifficultClimbingVRM.Patches
{
    internal static class PlayerSpawnerPatches
    {
        public static GameObject CurrentPlayerObject { get; private set; }
        public static GameObject PlayerPrefab { get; private set; }

        public static event Action<GameObject> PlayerSpawned = null;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSpawn), "SpawnPlayer")]
        [HarmonyPatch(typeof(PlayerSpawn), "Respawn")]
        static void SpawnPlayerVRM(GameObject ___p, GameObject ___player)
        {
            PlayerPrefab = ___player;

            CurrentPlayerObject = ___p;
            PlayerSpawned?.Invoke(CurrentPlayerObject);
        }
        //Return type of pass through postfix static bool DifficultClimbingVRM.PlayerSpawnerPatches.SpawnPlayerVRM(UnityEngine.GameObject& ___p) does not match type of its first parameter
    }
}
