using System.Windows;
using DekapuRescueTool.Services;
using DekapuRescueTool.ViewModels;
using DekapuRescueTool.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DekapuRescueTool;

public partial class App : Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogReaderService, LogReaderService>();
        services.AddSingleton<ISaveService, SaveService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<MainWindow>().Show();
        await sp.GetRequiredService<MainViewModel>().InitializeAsync();
    }
}
