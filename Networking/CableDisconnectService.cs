using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace MassCableRemover.Networking;

/// <summary>
/// Uses game dump (Assembly-CSharp): <see cref="NetworkSwitch.cableLinkSwitchPorts"/>, <see cref="PatchPanel.cableLinkPorts"/>,
/// <see cref="CableLink.SecondActionOnClick"/> (unplug), <see cref="NetworkMap.RemoveCableConnection"/>, then a final <see cref="NetworkSwitch.DisconnectCables"/> sweep on switches.
/// </summary>
public static class CableDisconnectService
{
    private const int MaxDisconnectCablesSweep = 64;

    public static bool TryDisconnectOnNetworkSwitch(NetworkSwitch sw)
    {
        if (sw == null)
            return false;

        try
        {
            var secondActions = TrySecondActionOnAllPorts(sw.cableLinkSwitchPorts);
            var mapRemoved = TryRemoveRegisteredCablesOnMap(sw.cableLinkSwitchPorts);
            var sweep = SweepDisconnectCables(sw);

            MelonLogger.Msg(
                $"[MassCableRemover] Switch: SecondAction ports={secondActions}, NetworkMap.RemoveCableConnection calls={mapRemoved}, DisconnectCables sweep={sweep}; any left={sw.IsAnyCableConnected()}.");

            return secondActions > 0 || mapRemoved > 0 || sweep > 0;
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error("[MassCableRemover] Switch disconnect failed: " + ex);
            return false;
        }
    }

    public static bool TryDisconnectOnPatchPanel(PatchPanel panel)
    {
        if (panel == null)
            return false;

        try
        {
            var secondActions = TrySecondActionOnAllPorts(panel.cableLinkPorts);
            var mapRemoved = TryRemoveRegisteredCablesOnMap(panel.cableLinkPorts);

            MelonLogger.Msg(
                $"[MassCableRemover] Patch panel: SecondAction ports={secondActions}, NetworkMap.RemoveCableConnection calls={mapRemoved}; any left={panel.IsAnyCableConnected()}.");

            return secondActions > 0 || mapRemoved > 0;
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error("[MassCableRemover] Patch panel disconnect failed: " + ex);
            return false;
        }
    }

    private static int TrySecondActionOnAllPorts(Il2CppReferenceArray<CableLink> ports)
    {
        if (ports == null)
            return 0;

        var n = 0;
        for (var i = 0; i < ports.Length; i++)
        {
            var link = ports[i];
            if (link == null)
                continue;

            try
            {
                if (!link.IsAllowedToDoSecondAction())
                    continue;
                link.SecondActionOnClick();
                n++;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("[MassCableRemover] SecondActionOnClick on a port failed (skipped): " + ex.Message);
            }
        }

        return n;
    }

    private static int TryRemoveRegisteredCablesOnMap(Il2CppReferenceArray<CableLink> ports)
    {
        var map = ResolveNetworkMap();
        if (map == null || ports == null)
            return 0;

        var ids = new HashSet<int>();
        for (var i = 0; i < ports.Length; i++)
        {
            var link = ports[i];
            if (link == null)
                continue;
            var id = link.cableIDsOnLink;
            if (id != 0)
                ids.Add(id);
        }

        var removed = 0;
        foreach (var id in ids)
        {
            try
            {
                map.RemoveCableConnection(id);
                removed++;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[MassCableRemover] RemoveCableConnection({id}) failed: " + ex.Message);
            }
        }

        return removed;
    }

    private static int SweepDisconnectCables(NetworkSwitch sw)
    {
        var sweep = 0;
        for (var i = 0; i < MaxDisconnectCablesSweep && sw.IsAnyCableConnected(); i++)
        {
            sw.DisconnectCables();
            sweep++;
        }

        return sweep;
    }

    private static NetworkMap ResolveNetworkMap()
    {
        var active = UnityEngine.Object.FindObjectOfType<NetworkMap>();
        if (active != null)
            return active;

        var all = Resources.FindObjectsOfTypeAll<NetworkMap>();
        if (all == null)
            return null;

        for (var i = 0; i < all.Length; i++)
        {
            var nm = all[i];
            if (nm == null)
                continue;
            if (nm.gameObject.scene.IsValid())
                return nm;
        }

        return null;
    }
}
