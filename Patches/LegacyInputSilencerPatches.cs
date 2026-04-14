using HarmonyLib;
using UnityEngine;

namespace MassCableRemover.Patches;

/// <summary>
/// Data Center still invokes legacy <see cref="Input"/> key APIs from Il2Cpp; with active input set to Input System only,
/// those calls throw every frame. Prefix them to return false and skip the original implementation.
/// </summary>
[HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
internal static class Input_GetKeyDown_Silencer
{
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Input), nameof(Input.GetKey), typeof(KeyCode))]
internal static class Input_GetKey_Silencer
{
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), typeof(KeyCode))]
internal static class Input_GetKeyUp_Silencer
{
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}
