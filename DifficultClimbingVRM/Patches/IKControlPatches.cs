using HarmonyLib;

namespace DifficultClimbingVRM.Patches
{
    internal static class IKControlPatches
    {
        public static float HandSurfaceDistanceL { get; private set; }
        public static float HandSurfaceDistanceR { get; private set; }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(IKControl), "SetTargets")]
        static void SetTargets(float ___handSurfaceDistance_R, float ___handSurfaceDistance_L)
        {
            HandSurfaceDistanceL = ___handSurfaceDistance_L;
            HandSurfaceDistanceR = ___handSurfaceDistance_R;
        }
    }
}
