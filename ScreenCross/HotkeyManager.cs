using System;
using System.Collections.Generic;

namespace ScreenCross;

/// <summary>
/// 全局热键：用 RegisterHotKey（系统正规注册方式，无键盘钩子，降低被误判风险）。
/// </summary>
public class HotkeyManager
{
    public const int IdToggle = 1;
    public const int IdLeft = 2;
    public const int IdRight = 3;
    public const int IdUp = 4;
    public const int IdDown = 5;

    private readonly IntPtr _hwnd;

    public HotkeyManager(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>注册全部热键，返回失败原因说明；全部成功时返回 null。</summary>
    public string? RegisterAll()
    {
        var failed = new List<string>();
        if (!NativeMethods.RegisterHotKey(_hwnd, IdToggle, NativeMethods.MOD_NONE, 0x77)) failed.Add("F8");
        if (!NativeMethods.RegisterHotKey(_hwnd, IdLeft, NativeMethods.MOD_CONTROL, 0x25)) failed.Add("Ctrl+←");
        if (!NativeMethods.RegisterHotKey(_hwnd, IdRight, NativeMethods.MOD_CONTROL, 0x27)) failed.Add("Ctrl+→");
        if (!NativeMethods.RegisterHotKey(_hwnd, IdUp, NativeMethods.MOD_CONTROL, 0x26)) failed.Add("Ctrl+↑");
        if (!NativeMethods.RegisterHotKey(_hwnd, IdDown, NativeMethods.MOD_CONTROL, 0x28)) failed.Add("Ctrl+↓");
        return failed.Count == 0 ? null : "以下热键可能被其他程序占用：" + string.Join("、", failed);
    }

    public void UnregisterAll()
    {
        for (int id = IdToggle; id <= IdDown; id++)
            NativeMethods.UnregisterHotKey(_hwnd, id);
    }
}
