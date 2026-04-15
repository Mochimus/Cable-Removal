using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MassCableRemover.Config;
using MassCableRemover.Networking;
using MassCableRemover.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MassCableRemover;

public sealed class Mod : MelonMod
{
    private const float WorldPurgeHoldSeconds = 10f;

    private HarmonyLib.Harmony _harmony;
    private bool _showMassRemoveHint;
    private bool _showWorldPurgeHint;

    private bool _charging;
    private float _chargeElapsed;
    private float _chargeHoldSeconds = 0.85f;
    private NetworkSwitch _chargeSwitch;
    private PatchPanel _chargePanel;

    private float _worldPurgeElapsed;

    public override void OnInitializeMelon()
    {
        InputBindSettings.EnsureLoaded();

        _harmony = new HarmonyLib.Harmony("MassCableRemover");
        try
        {
            _harmony.PatchAll(typeof(Mod).Assembly);
            MelonLogger.Msg("[MassCableRemover] Patched UnityEngine.Input GetKey/GetKeyDown/GetKeyUp so vanilla Il2Cpp paths do not throw under Input System-only mode.");
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning("[MassCableRemover] Legacy Input patches failed (log may still show Input errors): " + ex);
        }

        MelonLogger.Msg("[MassCableRemover] " + InputBindSettings.GetStartupBindingSummary() + " Ring uses the game's hold time, then all cables on that device are removed.");
        MelonLogger.Msg("[MassCableRemover] With no switch/patch panel targeted, hold the same keys for " + WorldPurgeHoldSeconds +
                        "s to remove ALL cables in the world (release keys or look at a device to cancel).");
#if WITH_GREGCORE
        MelonLogger.Msg("[MassCableRemover] Built with gregCore reference (WITH_GREGCORE).");
#endif
    }

    public override void OnUpdate()
    {
        _showMassRemoveHint = false;
        _showWorldPurgeHint = false;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || (InputBindSettings.ChargeUsesMouse() && mouse == null))
        {
            CancelCharge();
            CancelWorldPurge();
            return;
        }

        if (!InputBindSettings.IsAimHeld(kb))
        {
            CancelCharge();
            CancelWorldPurge();
            return;
        }

        if (LookTargetResolver.TryGetLookedAtCableDevice(out var sw, out var panel))
        {
            CancelWorldPurge();
            UpdateDeviceChargeFlow(kb, mouse, sw, panel);
            return;
        }

        CancelCharge();

        if (!InputBindSettings.ChargeIsPressed(kb, mouse))
        {
            CancelWorldPurge();
            return;
        }

        _worldPurgeElapsed += Time.deltaTime;
        _showWorldPurgeHint = true;

        if (_worldPurgeElapsed < WorldPurgeHoldSeconds)
            return;

        CableDisconnectService.TryDisconnectAllInWorld();
        CancelWorldPurge();
    }

    private void UpdateDeviceChargeFlow(Keyboard kb, Mouse mouse, NetworkSwitch sw, PatchPanel panel)
    {
        if (_charging && !IsSameChargeTarget(sw, panel))
            CancelCharge();

        _showMassRemoveHint = true;

        if (!_charging)
        {
            if (!InputBindSettings.ChargePressedThisFrame(kb, mouse))
                return;

            if (sw != null)
            {
                _chargeSwitch = sw;
                _chargePanel = null;
                _chargeHoldSeconds = InteractHoldDuration.GetSeconds(sw);
            }
            else
            {
                _chargePanel = panel;
                _chargeSwitch = null;
                _chargeHoldSeconds = InteractHoldDuration.GetSeconds(panel);
            }

            _chargeElapsed = 0f;
            _charging = true;
            return;
        }

        if (!InputBindSettings.ChargeIsPressed(kb, mouse))
        {
            CancelCharge();
            return;
        }

        _chargeElapsed += Time.deltaTime;
        if (_chargeElapsed < _chargeHoldSeconds)
            return;

        var s = _chargeSwitch;
        var p = _chargePanel;
        CancelCharge();

        if (s != null)
            CableDisconnectService.TryDisconnectOnNetworkSwitch(s);
        else if (p != null)
            CableDisconnectService.TryDisconnectOnPatchPanel(p);
    }

    private bool IsSameChargeTarget(NetworkSwitch sw, PatchPanel panel)
    {
        if (_chargeSwitch != null)
            return sw == _chargeSwitch;
        if (_chargePanel != null)
            return panel == _chargePanel;
        return false;
    }

    private void CancelCharge()
    {
        _charging = false;
        _chargeElapsed = 0f;
        _chargeSwitch = null;
        _chargePanel = null;
    }

    private void CancelWorldPurge()
    {
        _worldPurgeElapsed = 0f;
    }

    public override void OnGUI()
    {
        if (_showWorldPurgeHint)
        {
            const float w = 680f;
            const float h = 96f;
            var x = (Screen.width - w) * 0.5f;
            var y = Screen.height - 140f;

            GUI.depth = int.MinValue;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            var aim = InputBindSettings.GetAimHoldDisplayName().ToUpperInvariant();
            var chg = InputBindSettings.GetChargeHoldDisplayName();
            var msg = $"{aim}: WORLD purge — NOT looking at a device.\nKeep holding {chg} for {WorldPurgeHoldSeconds:0}s to remove ALL cables everywhere. Release keys or aim at a switch/panel to cancel.";
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, h - 12f), msg);

            var cx = Screen.width * 0.5f;
            var cy = Screen.height * 0.5f;
            var fill = Mathf.Clamp01(_worldPurgeElapsed / WorldPurgeHoldSeconds);
            MassRemoveChargeRing.Draw(cx, cy, 52f, fill);
            return;
        }

        if (!_showMassRemoveHint)
            return;

        const float w2 = 620f;
        const float h2 = 72f;
        var x2 = (Screen.width - w2) * 0.5f;
        var y2 = Screen.height - 130f;

        GUI.depth = int.MinValue;
        GUI.Box(new Rect(x2, y2, w2, h2), GUIContent.none);
        var aim2 = InputBindSettings.GetAimHoldDisplayName().ToUpperInvariant();
        var chg2 = InputBindSettings.GetChargeHoldDisplayName();
        var msg2 = _charging
            ? $"{aim2}: Removing ALL cables — keep holding {chg2} until the ring completes."
            : $"{aim2}: You are about to remove ALL cables from this device.\nHold {chg2} until the ring fills (same timing as unplugging one cable).";
        GUI.Label(new Rect(x2 + 12f, y2 + 10f, w2 - 24f, h2 - 16f), msg2);

        if (!_charging)
            return;

        var cx2 = Screen.width * 0.5f;
        var cy2 = Screen.height * 0.5f;
        var fill2 = _chargeHoldSeconds > 0.001f ? Mathf.Clamp01(_chargeElapsed / _chargeHoldSeconds) : 1f;
        MassRemoveChargeRing.Draw(cx2, cy2, 52f, fill2);
    }
}
