using System;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MassCableRemover.Config;

/// <summary>
/// Aim/charge keybinds loaded from <c>MassCableRemover_Keybinds.txt</c> in the game Mods folder (created on first run).
/// </summary>
public static class InputBindSettings
{
    public const string ConfigFileName = "MassCableRemover_Keybinds.txt";

    private static string _configFilePath;
    private static DateTime _loadedWriteTimeUtc = DateTime.MinValue;
    private static bool _loggedConfigPath;

    private static Key _resolvedAimKey = Key.LeftCtrl;
    private static ChargeBinding _resolvedCharge;
    private static bool _loggedAimFallback;
    private static bool _loggedChargeFallback;

    private enum ChargeBindingKind
    {
        RightMouse,
        LeftMouse,
        MiddleMouse,
        KeyboardKey,
    }

    private struct ChargeBinding
    {
        public ChargeBindingKind Kind;
        public Key Key;
    }

    /// <summary>Full path to the keybind file under Mods; set after <see cref="EnsureLoaded"/>.</summary>
    public static string ConfigFilePath => _configFilePath ?? string.Empty;

    public static void EnsureLoaded()
    {
        _configFilePath = Path.Combine(GetModsDirectory(), ConfigFileName);

        try
        {
            if (!File.Exists(_configFilePath))
                File.WriteAllText(_configFilePath, DefaultConfigFileBody);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[MassCableRemover] Could not create keybind file at " + _configFilePath + ": " + ex);
        }

        ReloadFromFile();

        if (!_loggedConfigPath && !string.IsNullOrEmpty(_configFilePath))
        {
            _loggedConfigPath = true;
            MelonLogger.Msg("[MassCableRemover] Keybind file: " + _configFilePath + " (edit while the game is closed, then restart.)");
        }
    }

    private static string GetModsDirectory()
    {
        try
        {
            var dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                var gameRoot = Directory.GetParent(dataPath)?.FullName;
                if (!string.IsNullOrEmpty(gameRoot))
                    return Path.Combine(gameRoot, "Mods");
            }
        }
        catch
        {
            /* Application.dataPath not ready */
        }

        var asmDir = Path.GetDirectoryName(typeof(MassCableRemover.Mod).Assembly.Location);
        return string.IsNullOrEmpty(asmDir) ? "Mods" : asmDir;
    }

    private static string DefaultConfigFileBody =>
        "# Mass Cable Remover — keybinds (edit while the game is closed, then restart)\r\n" +
        "# AimHoldKey: Unity Input System Key name (e.g. LeftCtrl, LeftShift, RightAlt)\r\n" +
        "AimHoldKey=LeftCtrl\r\n" +
        "# ChargeHold: RightMouse, LeftMouse, MiddleMouse, or a Key name (Space, E)\r\n" +
        "ChargeHold=RightMouse\r\n" +
        "# With no switch/patch panel in your crosshair, hold AimHoldKey + ChargeHold for 10s to disconnect ALL cables in the world.\r\n";

    public static void RefreshFromPreferences()
    {
        if (string.IsNullOrEmpty(_configFilePath))
            EnsureLoaded();

        if (string.IsNullOrEmpty(_configFilePath) || !File.Exists(_configFilePath))
            return;

        DateTime mtime;
        try
        {
            mtime = File.GetLastWriteTimeUtc(_configFilePath);
        }
        catch
        {
            return;
        }

        if (mtime == _loadedWriteTimeUtc)
            return;

        ReloadFromFile();
    }

    private static void ReloadFromFile()
    {
        if (string.IsNullOrEmpty(_configFilePath))
            return;

        string text;
        try
        {
            text = File.ReadAllText(_configFilePath);
            _loadedWriteTimeUtc = File.GetLastWriteTimeUtc(_configFilePath);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MassCableRemover] Could not read keybind file: " + ex.Message);
            return;
        }

        var aimRaw = "LeftCtrl";
        var chargeRaw = "RightMouse";
        foreach (var rawLine in text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                continue;

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line.Substring(0, eq).Trim();
            var val = line.Substring(eq + 1).Trim();
            if (key.Equals("AimHoldKey", StringComparison.OrdinalIgnoreCase))
                aimRaw = val;
            else if (key.Equals("ChargeHold", StringComparison.OrdinalIgnoreCase))
                chargeRaw = val;
        }

        ApplyBindings(aimRaw, chargeRaw);
    }

    private static void ApplyBindings(string aimRaw, string chargeRaw)
    {
        aimRaw = (aimRaw ?? string.Empty).Trim();
        chargeRaw = (chargeRaw ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(aimRaw) || !Enum.TryParse<Key>(aimRaw, true, out var aimKey) || aimKey == Key.None)
        {
            if (!_loggedAimFallback)
            {
                MelonLogger.Warning($"[MassCableRemover] Invalid AimHoldKey \"{aimRaw}\" — using LeftCtrl. See Unity Input System Key enum names.");
                _loggedAimFallback = true;
            }

            _resolvedAimKey = Key.LeftCtrl;
        }
        else
        {
            _resolvedAimKey = aimKey;
            _loggedAimFallback = false;
        }

        _resolvedCharge = ParseCharge(chargeRaw);
    }

    private static ChargeBinding ParseCharge(string chargeRaw)
    {
        if (string.IsNullOrEmpty(chargeRaw))
            return FallbackCharge(chargeRaw);

        var lower = chargeRaw.ToLowerInvariant();
        if (lower is "rightmouse" or "rightbutton" or "mouseright")
        {
            _loggedChargeFallback = false;
            return new ChargeBinding { Kind = ChargeBindingKind.RightMouse };
        }

        if (lower is "leftmouse" or "leftbutton" or "mouseleft")
        {
            _loggedChargeFallback = false;
            return new ChargeBinding { Kind = ChargeBindingKind.LeftMouse };
        }

        if (lower is "middlemouse" or "middlebutton" or "mousemiddle")
        {
            _loggedChargeFallback = false;
            return new ChargeBinding { Kind = ChargeBindingKind.MiddleMouse };
        }

        if (Enum.TryParse<Key>(chargeRaw, true, out var k) && k != Key.None)
        {
            _loggedChargeFallback = false;
            return new ChargeBinding { Kind = ChargeBindingKind.KeyboardKey, Key = k };
        }

        return FallbackCharge(chargeRaw);
    }

    private static ChargeBinding FallbackCharge(string chargeRaw)
    {
        if (!_loggedChargeFallback)
        {
            MelonLogger.Warning($"[MassCableRemover] Invalid ChargeHold \"{chargeRaw}\" — using RightMouse. Use RightMouse, LeftMouse, MiddleMouse, or a Key name.");
            _loggedChargeFallback = true;
        }

        return new ChargeBinding { Kind = ChargeBindingKind.RightMouse };
    }

    public static bool ChargeUsesMouse()
    {
        RefreshFromPreferences();
        return _resolvedCharge.Kind != ChargeBindingKind.KeyboardKey;
    }

    public static bool IsAimHeld(Keyboard kb)
    {
        if (kb == null)
            return false;
        RefreshFromPreferences();
        return kb[_resolvedAimKey].isPressed;
    }

    public static bool ChargePressedThisFrame(Keyboard kb, Mouse mouse)
    {
        RefreshFromPreferences();
        return _resolvedCharge.Kind switch
        {
            ChargeBindingKind.RightMouse => mouse != null && mouse.rightButton.wasPressedThisFrame,
            ChargeBindingKind.LeftMouse => mouse != null && mouse.leftButton.wasPressedThisFrame,
            ChargeBindingKind.MiddleMouse => mouse != null && mouse.middleButton.wasPressedThisFrame,
            ChargeBindingKind.KeyboardKey => kb != null && kb[_resolvedCharge.Key].wasPressedThisFrame,
            _ => false,
        };
    }

    public static bool ChargeIsPressed(Keyboard kb, Mouse mouse)
    {
        RefreshFromPreferences();
        return _resolvedCharge.Kind switch
        {
            ChargeBindingKind.RightMouse => mouse != null && mouse.rightButton.isPressed,
            ChargeBindingKind.LeftMouse => mouse != null && mouse.leftButton.isPressed,
            ChargeBindingKind.MiddleMouse => mouse != null && mouse.middleButton.isPressed,
            ChargeBindingKind.KeyboardKey => kb != null && kb[_resolvedCharge.Key].isPressed,
            _ => false,
        };
    }

    public static string GetAimHoldDisplayName()
    {
        RefreshFromPreferences();
        return FormatKeyName(_resolvedAimKey);
    }

    public static string GetChargeHoldDisplayName()
    {
        RefreshFromPreferences();
        return _resolvedCharge.Kind switch
        {
            ChargeBindingKind.RightMouse => "RIGHT MOUSE",
            ChargeBindingKind.LeftMouse => "LEFT MOUSE",
            ChargeBindingKind.MiddleMouse => "MIDDLE MOUSE",
            ChargeBindingKind.KeyboardKey => FormatKeyName(_resolvedCharge.Key).ToUpperInvariant(),
            _ => "RIGHT MOUSE",
        };
    }

    public static string GetStartupBindingSummary()
    {
        RefreshFromPreferences();
        return $"{GetAimHoldDisplayName()} + aim, hold {GetChargeHoldDisplayName()} to charge.";
    }

    private static string FormatKeyName(Key key)
    {
        var s = key.ToString();
        for (var i = 1; i < s.Length; i++)
        {
            if (char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                return s.Insert(i, " ");
        }

        return s;
    }
}
