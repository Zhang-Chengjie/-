using System;
using System.Windows;
using System.Windows.Interop;

namespace ScreenCross;

/// <summary>
/// 准星覆盖窗口：置顶、点击穿透、不抢焦点；
/// 仅在配置/位置/显示器变化时重绘一次，静止时零 CPU 占用；
/// 前台窗口切换时通过 WinEvent 事件重申置顶（零轮询）。
/// </summary>
public partial class OverlayWindow : Window
{
    private AppConfig _cfg = new();
    private IntPtr _hwnd;
    private double _scale = 1.0;
    private bool _crossVisible = true;
    private TrayIconManager? _tray;
    private HotkeyManager? _hotkeys;
    private SettingsWindow? _settings;
    private IntPtr _winEventHook = IntPtr.Zero;
    private readonly NativeMethods.WinEventProc _winEventCallback;

    /// <summary>外部（如热键微调）修改配置后通知设置窗口刷新。</summary>
    public event Action? ConfigExternallyChanged;

    public OverlayWindow()
    {
        InitializeComponent();
        _cfg = AppConfig.Load();
        _winEventCallback = OnWinEvent;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        // 点击穿透 + 不抢焦点 + 不进 Alt-Tab（F01/F03 关键实现）
        int ex = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE,
            new IntPtr(ex | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW));

        _hotkeys = new HotkeyManager(_hwnd);
        string? conflict = _hotkeys.RegisterAll();

        _tray = new TrayIconManager(_hwnd);
        _tray.ToggleRequested += ToggleVisible;
        _tray.SettingsRequested += OpenSettings;
        _tray.AutoStartToggleRequested += ToggleAutoStart;
        _tray.ExitRequested += () => Application.Current.Shutdown();
        UpdateTrayState();

        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventCallback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        ApplyAll();
        if (conflict != null)
            _tray.ShowBalloon("部分热键注册失败", conflict + "\n仍可通过托盘菜单操作。");
        Dispatcher.BeginInvoke(ShowFirstRunNotice);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            handled = true;
            switch (wParam.ToInt32())
            {
                case HotkeyManager.IdToggle: ToggleVisible(); break;
                case HotkeyManager.IdLeft: Nudge(-1, 0); break;
                case HotkeyManager.IdRight: Nudge(1, 0); break;
                case HotkeyManager.IdUp: Nudge(0, -1); break;
                case HotkeyManager.IdDown: Nudge(0, 1); break;
            }
        }
        else if (msg == NativeMethods.WM_APP_TRAY)
        {
            handled = true;
            _tray?.HandleTrayMessage(lParam);
        }
        else if (msg == NativeMethods.WM_DISPLAYCHANGE || msg == NativeMethods.WM_DPICHANGED)
        {
            ApplyAll();
        }
        return IntPtr.Zero;
    }

    private void OnWinEvent(IntPtr hook, uint eventId, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // 前台窗口变化时重申置顶（事件驱动，非轮询）
        if (eventId == NativeMethods.EVENT_SYSTEM_FOREGROUND && _crossVisible && _hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }

    public void ApplyAll()
    {
        ApplyPosition();
        RenderCrosshair();
    }

    /// <summary>配置发生变化（设置窗口或热键）后调用：持久化并刷新。</summary>
    public void ApplyConfigChange()
    {
        _cfg.Save();
        ApplyAll();
        UpdateTrayState();
        ConfigExternallyChanged?.Invoke();
    }

    private void ApplyPosition()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0) return;

        int idx = Math.Clamp(_cfg.MonitorIndex, 0, screens.Length - 1);
        var b = screens[idx].Bounds;
        int cx = b.Left + b.Width / 2;
        int cy = b.Top + b.Height / 2;

        double scale = 1.0;
        IntPtr hMon = NativeMethods.MonitorFromPoint(new NativeMethods.POINT(cx, cy), 2);
        if (hMon != IntPtr.Zero && NativeMethods.GetDpiForMonitor(hMon, 0, out uint dpiX, out _) == 0 && dpiX > 0)
            scale = dpiX / 96.0;
        bool scaleChanged = Math.Abs(scale - _scale) > 0.01;
        _scale = scale;

        int size = (int)Math.Round(401 * scale);
        int x = cx - size / 2 + _cfg.OffsetX;
        int y = cy - size / 2 + _cfg.OffsetY;

        if (IsLoaded)
        {
            NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, x, y, size, size,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOOWNERZORDER);
        }
        else
        {
            Left = x / scale;
            Top = y / scale;
            Width = 401;
            Height = 401;
        }

        if (scaleChanged) RenderCrosshair();
    }

    private void RenderCrosshair()
    {
        Root.Children.Clear();
        Root.Children.Add(CrosshairRenderer.Build(_cfg, _scale));
    }

    private void ToggleVisible()
    {
        if (_crossVisible) Hide();
        else
        {
            Show();
            ApplyPosition();
        }
        _crossVisible = !_crossVisible;
        UpdateTrayState();
    }

    private void Nudge(int dx, int dy)
    {
        _cfg.OffsetX = Math.Clamp(_cfg.OffsetX + dx, -500, 500);
        _cfg.OffsetY = Math.Clamp(_cfg.OffsetY + dy, -500, 500);
        ApplyPosition();
        _cfg.Save();
        ConfigExternallyChanged?.Invoke();
    }

    private void OpenSettings()
    {
        if (_settings != null && _settings.IsLoaded)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow(_cfg, this);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    private void ToggleAutoStart()
    {
        bool enable = !AutoStartHelper.IsEnabled();
        AutoStartHelper.SetEnabled(enable);
        _cfg.StartWithWindows = enable;
        _cfg.Save();
        UpdateTrayState();
    }

    private void UpdateTrayState() => _tray?.UpdateState(_crossVisible, AutoStartHelper.IsEnabled());

    private void ShowFirstRunNotice()
    {
        if (_cfg.FirstRunNoticeShown) return;
        _cfg.FirstRunNoticeShown = true;
        _cfg.Save();
        MessageBox.Show(this,
            "欢迎使用屏幕准星！\n\n" +
            "• F8：显示 / 隐藏准星\n" +
            "• Ctrl + 方向键：微调准星位置\n" +
            "• 右键点击托盘图标：打开菜单\n" +
            "• 左键点击托盘图标：打开设置\n\n" +
            "注意：部分游戏或平台可能限制第三方覆盖层，使用前请自行确认游戏条款。",
            "屏幕准星", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_winEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
        _hotkeys?.UnregisterAll();
        _tray?.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
