using System.Windows.Input;

namespace StarResonanceDpsAnalysis.Core.Extends.System;

public static class KeyExtension
{
    public static string KeyToString(this Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        if (key == Key.None)
            return string.Empty;

        // If no modifiers provided, use current keyboard state
        if (modifiers == ModifierKeys.None)
            modifiers = Keyboard.Modifiers;

        var result = new List<string>();

        // Check for modifier keys via ModifierKeys flags
        if (modifiers.HasFlag(ModifierKeys.Control))
            result.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt))
            result.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift))
            result.Add("Shift");

        // Exclude physical modifier keys from being shown as main key
        if (!key.IsAltKey() && !key.IsControlKey() && !key.IsShiftKey())
        {
            result.Add(key.ToString());
        }

        return string.Join("+", result);
    }

    public static bool IsControlKey(this Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl;
    }

    public static bool IsAltKey(this Key key)
    {
        return key is Key.LeftAlt or Key.RightAlt;
    }

    public static bool IsShiftKey(this Key key)
    {
        return key is Key.LeftShift or Key.RightShift;
    }

    /// <summary>
    /// Convert an integer (typically a WinForms Keys value) to a WPF Key.
    /// If main key is invalid, defaultKey is returned.
    /// Modifiers that are not allowed (per allowedKey) are cleared.
    /// Note: This returns only the main Key; modifiers are validated but not returned.
    /// </summary>
    /// <param name="value">Integer value encoding virtual-key and WinForms-style modifier bits</param>
    /// <param name="defaultKey">Fallback Key if conversion fails</param>
    /// <param name="allowedKey">Allowed ModifierKeys (defaults to Shift|Control|Alt)</param>
    /// <returns>WPF Key</returns>
    public static (Key key, ModifierKeys modifiers) IntToKey(this int value, Key defaultKey,
        ModifierKeys allowedKey = ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt)
    {
        const int keyCodeMask = 0xFFFF;

        var code = value & keyCodeMask;
        var extractedModifiers = value.ExtractModifiersFromInt();

        // Filter out disallowed modifiers
        var modifiers = extractedModifiers & allowedKey;

        // Convert virtual-key code to WPF Key
        try
        {
            var converted = KeyInterop.KeyFromVirtualKey(code);
            return (converted != Key.None ? converted : defaultKey, modifiers);
        }
        catch
        {
            return (defaultKey, modifiers);
        }
    }

    /// <summary>
    /// Extracts the modifier keys from an integer value based on predefined bitmask flags.
    /// </summary>
    /// <remarks>The method interprets the integer value using specific bitmask flags for the  <see
    /// cref="ModifierKeys.Control"/>, <see cref="ModifierKeys.Alt"/>, and <see cref="ModifierKeys.Shift"/>
    /// keys.</remarks>
    /// <param name="value">An integer representing a combination of modifier keys encoded as bit flags.</param>
    /// <returns>A <see cref="ModifierKeys"/> value representing the extracted modifier keys.  If no modifier keys are present,
    /// <see cref="ModifierKeys.None"/> is returned.</returns>
    public static ModifierKeys ExtractModifiersFromInt(this int value)
    {
        const int shiftMask = 0x10000, controlMask = 0x20000, altMask = 0x40000;

        var modifiers = ModifierKeys.None;
        if ((value & controlMask) != 0) modifiers |= ModifierKeys.Control;
        if ((value & altMask) != 0) modifiers |= ModifierKeys.Alt;
        if ((value & shiftMask) != 0) modifiers |= ModifierKeys.Shift;

        return modifiers;
    }


    /// <summary>
    /// Parse a shortcut string like "Ctrl+Shift+A" into a Key and ModifierKeys.
    /// </summary>
    /// <param name="shortcut"></param>
    public static (Key key, ModifierKeys modifiers) ParseShortcutString(this string shortcut)
    {
        var key = Key.None;
        var modifiers = ModifierKeys.None;

        if (string.IsNullOrEmpty(shortcut)) return (key, modifiers);

        foreach (var part in shortcut.Split('+').Select(p => p.Trim()))
        {
            switch (part.ToLower())
            {
                case "ctrl": modifiers |= ModifierKeys.Control; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                default:
                    if (Enum.TryParse<Key>(part, true, out var parsedKey))
                        key = parsedKey;
                    break;
            }
        }

        return (key, modifiers);
    }
}