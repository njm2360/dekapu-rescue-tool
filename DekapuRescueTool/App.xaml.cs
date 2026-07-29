using System.Windows;
using System.Windows.Threading;
using DekapuRescueTool.Services;
using DekapuRescueTool.ViewModels;
using DekapuRescueTool.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DekapuRescueTool;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<ILogReaderService, LogReaderService>();
        services.AddSingleton<ISaveService, SaveService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<MainWindow>().Show();

        try
        {
            await sp.GetRequiredService<MainViewModel>().InitializeAsync();
        }
        catch (Exception ex)
        {
            HandleFatal("Startup", ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleFatal("DispatcherUnhandledException", e.Exception);
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        CrashLogger.Log("AppDomainUnhandledException", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashLogger.Log("UnobservedTaskException", e.Exception);
    }

    private void HandleFatal(string source, Exception ex)
    {
        CrashLogger.Log(source, ex);

        MessageBox.Show(
            $"予期しないエラーが発生しました。アプリケーションを終了します。\n\n{ex.Message}\n\n" +
            $"エラーログ\n{CrashLogger.LogPath}",
            "エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Shutdown(1);
    }
}
