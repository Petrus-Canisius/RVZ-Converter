using System.Globalization;
using Avalonia;

namespace RvzConverter.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        if (args.Length == 2 && args[0] == "--verify-package")
            return RvzConverter.Core.PackageVerification.RunAsync(AppContext.BaseDirectory, args[1]).GetAwaiter().GetResult();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
