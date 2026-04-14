using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MassCableRemover.Networking;
using MassCableRemover.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MassCableRemover;

public sealed class Mod : MelonMod
{
    private HarmonyLib.Harmony _harmony;
    private bool _showMassRemoveHint;

    private bool _charging;
    private float _chargeElapsed;
    private float _chargeHoldSeconds = 0.85f;
    private NetworkSwitch _chargeSwitch;
    private PatchPanel _chargePanel;

    public override void OnInitializeMelon()
    {
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

        MelonLogger.Msg("[MassCableRemover] CTRL + aim at switch/patch panel: warning text. Hold RIGHT MOUSE (CTRL still held); ring fills using the game's hold time, then all cables on that device are removed.");
#if WITH_GREGCORE
        MelonLogger.Msg("[MassCableRemover] Built with gregCore reference (WITH_GREGCORE).");
#endif
    }

    public override void OnUpdate()
    {
        _showMassRemoveHint = false;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null)
        {
            CancelCharge();
            return;
        }

        var ctrlHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        if (!ctrlHeld)
        {
            CancelCharge();
            return;
        }

        if (!LookTargetResolver.TryGetLookedAtCableDevice(out var sw, out var panel))
        {
            CancelCharge();
            return;
        }

        if (_charging && !IsSameChargeTarget(sw, panel))
            CancelCharge();

        _showMassRemoveHint = true;

        if (!_charging)
        {
            if (!mouse.rightButton.wasPressedThisFrame)
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

        if (!mouse.rightButton.isPressed)
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

    public override void OnGUI()
    {
        if (!_showMassRemoveHint)
            return;

        const float w = 620f;
        const float h = 72f;
        var x = (Screen.width - w) * 0.5f;
        var y = Screen.height - 130f;

        GUI.depth = int.MinValue;
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);
        var msg = _charging
            ? "CTRL: Removing ALL cables — keep holding RIGHT MOUSE until the ring completes."
            : "CTRL: You are about to remove ALL cables from this device.\nHold RIGHT MOUSE until the ring fills (same timing as unplugging one cable).";
        GUI.Label(new Rect(x + 12f, y + 10f, w - 24f, h - 16f), msg);

        if (!_charging)
            return;

        var cx = Screen.width * 0.5f;
        var cy = Screen.height * 0.5f;
        var fill = _chargeHoldSeconds > 0.001f ? Mathf.Clamp01(_chargeElapsed / _chargeHoldSeconds) : 1f;
        MassRemoveChargeRing.Draw(cx, cy, 52f, fill);
    }
}
