using System;
using System.Threading;
using System.Windows;

namespace ScreenCross;

public partial class App : Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, @"Local\ScreenCross.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("屏幕准星已在运行，请查看系统托盘图标。", "屏幕准星",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("发生未处理的错误：" + args.Exception.Message, "屏幕准星",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var overlay = new OverlayWindow();
        overlay.Show();

        // 启动完成（JIT 结束）后归还多余物理内存，降低常驻占用
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { NativeMethods.SetProcessWorkingSetSize(new IntPtr(-1), new IntPtr(-1), new IntPtr(-1)); }
            catch { /* 非关键路径 */ }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
