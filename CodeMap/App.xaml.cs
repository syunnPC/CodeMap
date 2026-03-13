using CodeMap.Diagnostics;
using Microsoft.UI.Xaml;
using System;

namespace CodeMap;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        ConsoleBootstrap.EnsureConsole(AppLaunchOptions.Current.EnableConsoleDebugging);
        InitializeComponent();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [CodeMap] App initialized.");
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
