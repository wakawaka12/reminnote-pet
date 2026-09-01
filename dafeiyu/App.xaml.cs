using System.IO;
using System.Windows;
using System.Windows.Threading;
using dafeiyu.Startup;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace dafeiyu;

public partial class App : Application
{
    private PetSingleInstanceCoordinator? _singleInstance;
    private PetWindow? _petWindow;

    /// <summary>崩溃日志路径：%LOCALAPPDATA%\dafeiyu\crash.log</summary>
    private static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "dafeiyu",
        "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局异常兜底：记录堆栈到崩溃日志，避免未处理异常直接“闪退”且无从排查。
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        try
        {
            // 单实例：若已有桌宠在运行，则唤起它并退出本进程。
            _singleInstance = new PetSingleInstanceCoordinator();
            if (!_singleInstance.IsPrimary)
            {
                _singleInstance.TryActivateExisting();
                Shutdown(0);
                return;
            }

            _singleInstance.StartListener(() =>
                Dispatcher.BeginInvoke(new Action(() => _petWindow?.ShowFromTray())));

            _petWindow = new PetWindow();
            MainWindow = _petWindow;
            _petWindow.Show();
        }
        catch (Exception exception)
        {
            LogCrash(exception, title: "启动失败");
            MessageBox.Show(
                $"桌宠启动失败：{exception.Message}",
                "大肥鱼桌宠",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        // 记录后继续运行，避免 UI 线程异常导致闪退；后续可据此修复。
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        LogCrash(e.ExceptionObject as Exception);
    }

    private static void LogCrash(Exception? exception, string title = "未处理异常")
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(
                CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}: {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}
