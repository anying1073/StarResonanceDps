using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Helpers;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Services;

public sealed partial class GlobalHotkeyService(
    ILogger<GlobalHotkeyService> logger,
    IConfigManager configManager,
    IMousePenetrationService mousePenetration,
    ITopmostService topmostService,
    IWindowManagementService windowManager,
    DpsStatisticsViewModel dpsStatisticsViewModel,
    PersonalDpsViewModel personalDpsViewModel)
    : IGlobalHotkeyService
{
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WH_KEYBOARD_LL = 13;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    private AppConfig _config = configManager.CurrentConfig;
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private LowLevelKeyboardProc? _keyboardProc;
    private readonly List<Key> _pressedKeys = new();

    public void Start()
    {
        SetHook();
        configManager.ConfigurationUpdated += OnConfigUpdated;
    }

    public void Stop()
    {
        try
        {
            UnHook();
            configManager.ConfigurationUpdated -= OnConfigUpdated;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stop failed");
        }
    }

    public void UpdateFromConfig(AppConfig config)
    {
        _config = config;
    }

    private void OnConfigUpdated(object? sender, AppConfig e)
    {
        UpdateFromConfig(e);
    }

    private void SetHook()
    {
        if (_keyboardHookHandle != IntPtr.Zero) return;

        try
        {
            _keyboardProc = KeyboardHookProc;
            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule;
            var hModule = module != null ? GetModuleHandle(module.ModuleName) : IntPtr.Zero;
            _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hModule, 0);
            
            if (_keyboardHookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                logger.LogWarning("Failed to install keyboard hook. Win32Error={Error}", error);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SetHook failed");
        }
    }

    private void UnHook()
    {
        if (_keyboardHookHandle == IntPtr.Zero) return;

        try
        {
            UnhookWindowsHookEx(_keyboardHookHandle);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "UnHook failed");
        }
        finally
        {
            _keyboardHookHandle = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var key = KeyInterop.KeyFromVirtualKey((int)hookStruct.vkCode);

            if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
            {
                if (IsCtrlAltShiftKey(key) && !_pressedKeys.Contains(key))
                {
                    _pressedKeys.Add(key);
                }

                var keyData = GetDownKeys(key);

                if (IsHotkeyMatch(_config.MouseThroughShortcut, keyData))
                {
                    ToggleMouseThrough();
                }
                else if (IsHotkeyMatch(_config.TopmostShortcut, keyData))
                {
                    ToggleTopmost();
                }
                else if (IsHotkeyMatch(_config.ClearDataShortcut, keyData))
                {
                    TriggerReset();
                }
            }
            else if (wParam == (IntPtr)0x0101 || wParam == (IntPtr)0x0105) // WM_KEYUP || WM_SYSKEYUP
            {
                if (IsCtrlAltShiftKey(key))
                {
                    _pressedKeys.Remove(key);
                }
            }
        }

        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private Key GetDownKeys(Key key)
    {
        var modifiers = ModifierKeys.None;

        foreach (var pressedKey in _pressedKeys)
        {
            if (pressedKey == Key.LeftCtrl || pressedKey == Key.RightCtrl)
                modifiers |= ModifierKeys.Control;
            if (pressedKey == Key.LeftAlt || pressedKey == Key.RightAlt)
                modifiers |= ModifierKeys.Alt;
            if (pressedKey == Key.LeftShift || pressedKey == Key.RightShift)
                modifiers |= ModifierKeys.Shift;
        }

        return CombineKeyWithModifiers(key, modifiers);
    }

    private Key CombineKeyWithModifiers(Key key, ModifierKeys modifiers)
    {
        var result = key;
        if (modifiers.HasFlag(ModifierKeys.Control))
            result |= (Key)((int)ModifierKeys.Control << 16);
        if (modifiers.HasFlag(ModifierKeys.Alt))
            result |= (Key)((int)ModifierKeys.Alt << 16);
        if (modifiers.HasFlag(ModifierKeys.Shift))
            result |= (Key)((int)ModifierKeys.Shift << 16);
        return result;
    }

    private bool IsCtrlAltShiftKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift;
    }

    private bool IsHotkeyMatch(Models.KeyBinding binding, Key keyData)
    {
        if (binding.Key == Key.None) return false;

        var targetKey = CombineKeyWithModifiers(binding.Key, binding.Modifiers);
        return targetKey == keyData;
    }

    private void ToggleMouseThrough()
    {
        try
        {
            var newState = !_config.MouseThroughEnabled;
            _config.MouseThroughEnabled = newState;
            MouseThroughHelper.ApplyToCoreWindows(_config, windowManager, mousePenetration);
            _ = configManager.SaveAsync(_config);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ToggleMouseThrough failed");
        }
    }

    private void ToggleTopmost()
    {
        try
        {
            var newState = !_config.TopmostEnabled;
            _config.TopmostEnabled = newState;

            topmostService.SetTopmost(windowManager.DpsStatisticsView, newState);
            topmostService.SetTopmost(windowManager.PersonalDpsView, newState);

            _ = configManager.SaveAsync(_config);

            logger.LogInformation(
                "TopMostService: Top most state changed to {State}",
                newState ? "Enabled" : "Disabled");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ToggleTopmost failed");
        }
    }

    private void TriggerReset()
    {
        try
        {
            personalDpsViewModel.Clear();
            dpsStatisticsViewModel.ResetAll();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TriggerReset failed");
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static partial IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "UnhookWindowsHookEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "CallNextHookEx")]
    private static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
}