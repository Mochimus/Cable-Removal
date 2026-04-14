using Il2Cpp;
using UnityEngine;

namespace MassCableRemover.Networking;

/// <summary>
/// Uses vanilla <see cref="Interact.holdDuration"/> / <see cref="Interact.timeForAction"/> from any port <see cref="CableLink"/> on the device.
/// </summary>
internal static class InteractHoldDuration
{
    private const float FallbackSeconds = 0.85f;

    public static float GetSeconds(NetworkSwitch sw)
    {
        if (sw?.cableLinkSwitchPorts == null)
            return FallbackSeconds;

        for (var i = 0; i < sw.cableLinkSwitchPorts.Length; i++)
        {
            var link = sw.cableLinkSwitchPorts[i];
            var s = SecondsFromLink(link);
            if (s > 0f)
                return s;
        }

        return FallbackSeconds;
    }

    public static float GetSeconds(PatchPanel panel)
    {
        if (panel?.cableLinkPorts == null)
            return FallbackSeconds;

        for (var i = 0; i < panel.cableLinkPorts.Length; i++)
        {
            var link = panel.cableLinkPorts[i];
            var s = SecondsFromLink(link);
            if (s > 0f)
                return s;
        }

        return FallbackSeconds;
    }

    private static float SecondsFromLink(CableLink link)
    {
        if (link == null)
            return 0f;

        if (link.holdDuration > 0.001f)
            return link.holdDuration;

        if (link.timeForAction > 0.001f)
            return link.timeForAction;

        return 0f;
    }
}
