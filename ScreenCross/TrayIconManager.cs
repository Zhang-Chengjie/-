using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ScreenCross;

/// <summary>系统托盘图标与右键菜单（Win32 原生实现，零 NuGet 依赖）。</summary>
public class TrayIconManager : IDisposable
{
    private const int IdToggle = 1;
    private const int IdSettings = 2;
    private const int IdAutoStart = 3;
    private const int IdExit = 4;

    private readonly IntPtr _hwnd;
    private readonly Icon _icon;
    private bool _added;
    private bool _visible = true;
    private bool _autoStart;

    public event Action? ToggleRequested;
    public event Action? SettingsRequested;
    public event Action? AutoStartToggleRequested;
    public event Action? ExitRequested;

    public TrayIconManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _icon = CreateTrayIcon();

        var nid = NewData(NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP);
        nid.uCallbackMessage = (uint)NativeMethods.WM_APP_TRAY;
        nid.hIcon = _icon.Handle;
        nid.szTip = "屏幕准星";
        if (NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref nid))
            _added = true;
    }

    public void UpdateState(bool visible, bool autoStart)
    {
        _visible = visible;
        _autoStart = autoStart;
    }

    public void ShowBalloon(string title, string text)
    {
        if (!_added) return;
        var nid = NewData(NativeMethods.NIF_INFO);
        nid.szInfoTitle = title;
        nid.szInfo = text;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);
    }

    public void HandleTrayMessage(IntPtr lParam)
    {
        int m = lParam.ToInt32();
        if (m == 0x0205 || m == 0x007B)              // WM_RBUTTONUP / WM_CONTEXTMENU
            ShowContextMenu();
        else if (m == 0x0202 || m == 0x0203)         // WM_LBUTTONUP / WM_LBUTTONDBLCLK
            SettingsRequested?.Invoke();
    }

    public void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING | (_visible ? NativeMethods.MF_CHECKED : 0),
            (UIntPtr)IdToggle, _visible ? "隐藏准星 (F8)" : "显示准星 (F8)");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (UIntPtr)IdSettings, "打开设置");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING | (_autoStart ? NativeMethods.MF_CHECKED : 0),
            (UIntPtr)IdAutoStart, "开机自动运行");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (UIntPtr)IdExit, "退出");

        NativeMethods.GetCursorPos(out var p);
        NativeMethods.SetForegroundWindow(_hwnd);
        int cmd = NativeMethods.TrackPopupMenu(menu,
            NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON, p.X, p.Y, 0, _hwnd, IntPtr.Zero);
        NativeMethods.DestroyMenu(menu);

        switch (cmd)
        {
            case IdToggle: ToggleRequested?.Invoke(); break;
            case IdSettings: SettingsRequested?.Invoke(); break;
            case IdAutoStart: AutoStartToggleRequested?.Invoke(); break;
            case IdExit: ExitRequested?.Invoke(); break;
        }
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(0, 220, 60), 3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, 5, 16, 12, 16);
            g.DrawLine(pen, 20, 16, 27, 16);
            g.DrawLine(pen, 16, 5, 16, 12);
            g.DrawLine(pen, 16, 20, 16, 27);
            g.FillEllipse(Brushes.Lime, 14, 14, 4, 4);
        }
        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon); // 进程存续期内保持有效
    }

    private NativeMethods.NOTIFYICONDATA NewData(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    public void Dispose()
    {
        if (_added)
        {
            var nid = NewData(0);
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref nid);
            _added = false;
        }
    }
}
