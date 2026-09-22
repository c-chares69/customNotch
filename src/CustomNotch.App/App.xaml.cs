using System.IO;
using System.Windows;
using CustomNotch.Core;
using CustomNotch.Core.Platform;

namespace CustomNotch.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private Controller? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--version"))
        {
            Console.WriteLine($"{Core.App.Name} {Core.App.Version}");
            Shutdown();
            return;
        }
        var homeArg = Array.IndexOf(e.Args, "--home");
        if (homeArg >= 0 && homeArg + 1 < e.Args.Length) Environment.SetEnvironmentVariable(Core.App.HomeEnv, e.Args[homeArg + 1]);
        var home = Paths.Home();
        Directory.CreateDirectory(home);
        Log.Directory = Paths.LogDir(home);
        _instance = new SingleInstance(home);
        if (!_instance.Acquire())
        {
            SingleInstance.Ping(home, "show");
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("app", $"Exception non gérée : {ex.Exception}");
            ex.Handled = true;
        };
        // Hors du thread UI (une boucle de Scheduler tourne sur un Timer, une source sur un Task) : sans ces
        // deux gardes, une exception qui s'en échappe tue le processus entier sans passer par
        // DispatcherUnhandledException, qui ne voit que le thread UI.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("app", $"Exception non gérée (hors UI) : {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("app", $"Tâche non observée : {e.Exception}"); e.SetObserved(); };
        _controller = new Controller(home);
        _instance.ShowRequested += () => Dispatcher.BeginInvoke(() => _controller.ShowSettings());
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Stop();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
