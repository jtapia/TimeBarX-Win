using Avalonia;
using System;
using System.Linq;

namespace TimeBarX.App;

class Program
{
    public static SingleInstance? Instance { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        var uriArg = args.FirstOrDefault(a => a.StartsWith("timebarx://", StringComparison.OrdinalIgnoreCase));

        var instance = new SingleInstance();
        if (!instance.IsOwner)
        {
            // Forward URI (if any) to the running instance and exit. Without a URI,
            // we still exit silently — second launches are no-ops.
            if (uriArg is not null)
            {
                SingleInstance.TrySend(uriArg, TimeSpan.FromSeconds(2));
            }
            instance.Dispose();
            return 0;
        }

        Instance = instance;
        Instance.StartListener();

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            instance.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
