using Il2Cpp;
using UnityEngine;

namespace MassCableRemover.Networking;

/// <summary>
/// Resolves what switch or patch panel the player is looking at (viewport center ray).
/// </summary>
internal static class LookTargetResolver
{
    private const float MaxDistance = 14f;

    public static bool TryGetLookedAtCableDevice(out NetworkSwitch networkSwitch, out PatchPanel patchPanel)
    {
        networkSwitch = null;
        patchPanel = null;

        var cam = Camera.main;
        if (cam == null)
            return false;

        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out var hit, MaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider == null)
            return false;

        networkSwitch = hit.collider.GetComponentInParent<NetworkSwitch>();
        if (networkSwitch != null)
            return true;

        patchPanel = hit.collider.GetComponentInParent<PatchPanel>();
        return patchPanel != null;
    }
}
