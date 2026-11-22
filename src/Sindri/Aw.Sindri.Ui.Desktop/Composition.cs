using Avalonia;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Views;
using Aw.UiDriver.Avalonia;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The head's composition, by hand: one adapter per Aw.Sindri.Ui port, bundled as a <see cref="SindriPlatform"/> and handed
/// to the app. There is no container: a service locator would sit outside a Host (ARCH-DI-001).
/// </summary>
internal static class Composition
{
    /// <summary>How often the OS reduce-motion setting is re-read.</summary>
    internal static readonly TimeSpan MotionPoll = TimeSpan.FromSeconds(2);

    /// <summary>The desktop adapters, over the files <see cref="ControlFilePath"/> and <see cref="UiStatePaths"/> name.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="userHome">The user's home directory.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <returns>The adapters.</returns>
    internal static SindriPlatform Platform(Func<string, string?> environment, string userHome, TimeProvider clock)
    {
        var ui = UiStatePaths.From(environment, userHome);
        return new SindriPlatform(
            new FileEndpointRecords(ControlFilePath.From(environment, userHome)),
            new FileHumanToken(ControlFilePath.TokenFrom(environment, userHome)),
            new FilePreferencesStore(ui.Preferences),
#pragma warning disable CA2000 // the motion poll, the single-instance claim and the tray live for the process: the platform record is the app's for its lifetime
            new PolledMotionPreference(MotionReader(), clock, MotionPoll),
            new SocketSingleInstance(ui, clock),
            new AvaloniaTray())
#pragma warning restore CA2000
        {
            DockBadge = DockBadge(),
        };
    }

    /// <summary>This OS's dock badge: macOS NSDockTile, the Windows taskbar overlay on the main window; elsewhere none (Linux has no common one).</summary>
    /// <returns>The badge.</returns>
    internal static IDockBadge DockBadge()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new MacDockBadge();
        }

        return OperatingSystem.IsWindows() ? new WindowsDockBadge(Aw.Sindri.Ui.Views.MainWindowHandle.Current) : new NullDockBadge();
    }

    /// <summary>The app over <paramref name="platform"/>.</summary>
    /// <param name="platform">The head's adapters.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <param name="args">The command line; <code language="csharp">--ui-driver</code> (like AW_UI_DRIVER=1) makes the app drivable by aw-ui-driver, which desktop UI evidence uses. A normal launch opens no port.</param>
    /// <returns>The app's builder.</returns>
    internal static AppBuilder Compose(SindriPlatform platform, TimeProvider clock, IReadOnlyList<string> args) =>
        AppBuilder.Configure(() => SindriApp.Create(platform, clock))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseAwUiDriver(args);

    /// <summary>This OS's reduce-motion reader: macOS NSWorkspace, Windows SPI_GETCLIENTAREAANIMATION, GNOME enable-animations; elsewhere never.</summary>
    /// <returns>The reader.</returns>
    internal static Func<bool> MotionReader()
    {
        if (OperatingSystem.IsMacOS())
        {
            return MacMotion.Read;
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsMotion.Read;
        }

        return OperatingSystem.IsLinux() ? static () => GnomeMotion.Read(GnomeMotion.Command) : static () => false;
    }
}
