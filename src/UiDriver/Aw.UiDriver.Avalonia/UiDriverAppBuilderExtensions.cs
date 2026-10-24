using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// The one call an Avalonia app makes to be drivable: <code language="csharp">AppBuilder.Configure&lt;App&gt;().UseAwUiDriver(args)</code>. It does nothing unless the run asks for it,
/// with AW_UI_DRIVER=1 in the environment or --ui-driver on the command line; a normal launch never opens a port. When asked, a loopback HTTP server
/// starts after Avalonia is set up and the directory AW_UI_DRIVER_DIR (else a new folder in the temp directory) gets driver.json (the address) and
/// driver.token (the per-run secret, owner-only); the line "aw-ui-driver: listening, dir &lt;dir&gt;" goes to standard error.
/// </summary>
public static class UiDriverAppBuilderExtensions
{
    private const string EnableVariable = "AW_UI_DRIVER";
    private const string DirectoryVariable = "AW_UI_DRIVER_DIR";
    private const string EnableFlag = "--ui-driver";

    /// <summary>Makes the app drivable when the run asks for it.</summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="args">The command line, for --ui-driver; none looks only at the environment.</param>
    /// <returns>The same builder.</returns>
    public static AppBuilder UseAwUiDriver(this AppBuilder builder, IReadOnlyList<string>? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var asked = string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal) || (args?.Contains(EnableFlag, StringComparer.Ordinal) ?? false);
        return asked ? builder.AfterSetup(static _ => DriverRuntime.Start(Environment.GetEnvironmentVariable(DirectoryVariable), MainWindow)) : builder;
    }

    // The app's main window: the classic desktop lifetime's, else its first open window.
    private static Window? MainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow ?? (desktop.Windows.Count > 0 ? desktop.Windows[0] : null) : null;
}
