using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The Sindri app: the theme, and on a desktop the shell window following the control plane (UI spec §3.2, §15). A head
/// creates it with <see cref="Create(SindriPlatform, TimeProvider)"/>, passing its platform adapters; the parameterless
/// constructor (theme only) is for the XAML loader and headless view tests. The platform wiring (tray, single instance,
/// close-to-tray) is another part of this class, which receives the window through ShellOpened.
/// </summary>
public sealed partial class SindriApp : Application, IDisposable
{
    private readonly Preferences? _preferences;
    private readonly TimeProvider? _clock;
    private InstanceListener? _listener;
    private readonly CancellationTokenSource _listening = new();
    private int _disposed;

    /// <summary>
    /// The exit code when the app failed (SND-UI-004): its start-up, or its following of the control plane, threw. The app
    /// logs the code and the exception's type, then ends rather than lingering without a window or tray.
    /// </summary>
    public const int FailedExitCode = 2;

    /// <summary>The app with its theme only: no window, nothing followed.</summary>
    public SindriApp()
    {
    }

    private SindriApp(ShellViewModel shell, Preferences preferences, SindriPlatform platform, TimeProvider clock)
    {
        Shell = shell;
        _preferences = preferences;
        _clock = clock;
        Platform = platform;
    }

    /// <summary>The app in the shell (UI spec §3.2, §15): the shell window over <paramref name="platform"/>'s adapters.</summary>
    /// <param name="platform">The head's adapters.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <returns>The app.</returns>
    public static SindriApp Create(SindriPlatform platform, TimeProvider clock)
    {
        // AppBuilder.Configure runs this factory before Avalonia installs its synchronization context, and the view models
        // capture the current one when they are built: install it first, or every timer callback would run on its own thread.
        Avalonia.Threading.AvaloniaSynchronizationContext.InstallIfNeeded();

        // One writer of the preferences for the whole app: the shell and Settings share it, so their writes queue.
        var preferences = new Preferences(platform.Preferences);
        var clipboard = new AvaloniaClipboard(static () => (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        var launcher = new AvaloniaLauncher(static () => (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
#pragma warning disable CA2000 // the shell view model keeps the client for the app's process
        var api = new SindriApiClient(Loopback(), platform.Token, clock, platform.Log);
#pragma warning restore CA2000
        var folders = new AvaloniaFolderPicker(static () => (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        var files = new AvaloniaFilePicker(static () => (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        return new(new ShellViewModel(platform.Records, api, clock, preferences, clipboard, platform.Motion, new WindowPorts(launcher, files, folders)), preferences, platform, clock);
    }

    /// <summary>Stops reading the single instance's messages (the app's exit does; a test that starts the app calls it).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _listening.Cancel();
            _listening.Dispose();
        }
    }

    /// <summary>The shell the app was built over; null for the theme-only app.</summary>
    internal ShellViewModel? Shell { get; }

    /// <summary>The head's adapters, or null for the theme-only app.</summary>
    internal SindriPlatform? Platform { get; }

    /// <summary>
    /// Ends the app with an exit code: the lifetime's Shutdown. A headless test replaces it, since the real one also shuts
    /// the UI thread's dispatcher down, which the test session shares.
    /// </summary>
    internal Action<IControlledApplicationLifetime, int> End { get; set; } = static (lifetime, exitCode) => lifetime.Shutdown(exitCode);

    // The API is on loopback: never through a configured proxy (as the CLI's client), and a connect that hangs fails in 2 s.
    // The client owns no timeout of its own, so the event stream stays open.
    private static HttpClient Loopback() =>
#pragma warning disable CA2000 // the client lives for the app's process
        new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(2) }) { Timeout = Timeout.InfiniteTimeSpan };

#pragma warning restore CA2000
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (Shell is not null && _preferences is not null && Platform is { } platform && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // A later launch is answered from now on, while the window is still being built (ShellOpened hands the
            // messages to the window's lifetime); reading ends when the head disposes the instance.
            _listener = new InstanceListener(platform.Instance);
            _ = _listener.Listen(_listening.Token);
            desktop.Exit += (_, _) => Dispose();

            // Started on the UI thread, so the view models' awaits come back to it. Its failure is observed there: logged,
            // then the app ends with FailedExitCode.
            _ = Open(desktop, Shell, _preferences, platform).ContinueWith(
                opened => Fail(desktop, platform, opened.Exception),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        base.OnFrameworkInitializationCompleted();
    }

    // The window opens once the preferences are read (the text scale and reduced motion, applied before the first frame;
    // its bounds, the sheet width and the last page), then follows.
    private async Task Open(IClassicDesktopStyleApplicationLifetime desktop, ShellViewModel shell, Preferences preferences, SindriPlatform platform)
    {
        var settings = await Settings(shell, preferences, platform).ConfigureAwait(true);
        desktop.Exit += (_, _) => settings.Dispose();
        await shell.Restore(CancellationToken.None).ConfigureAwait(true);
        var window = new ShellWindow { DataContext = shell };
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        SettingsTheme.Follow(window, settings);
        desktop.MainWindow = window;
        window.Show();
        ShellOpened(desktop, window, shell);
        await shell.Run(CancellationToken.None).ConfigureAwait(true);
    }

    // The code and the exception's type only: its message and stack can carry paths and payloads.
    private void Fail(IClassicDesktopStyleApplicationLifetime desktop, SindriPlatform platform, AggregateException? failure)
    {
        var cause = failure?.InnerException ?? failure;
        SindriAppLog.Failed(platform.Log, cause?.GetType().Name ?? "unknown");
        End(desktop, FailedExitCode);
    }

    // Settings (⌘,) over the preferences and the OS setting, applying to this app's resources and the shell's lists. A preferences file that
    // can't be read leaves the theme's own values (the defaults) in place.
    private async Task<SettingsViewModel> Settings(ShellViewModel shell, Preferences preferences, SindriPlatform platform)
    {
        var theme = SettingsTheme.For(Resources);
        var settings = new SettingsViewModel(preferences, platform.Motion, appearance =>
        {
            theme(appearance);
            shell.ApplyAppearance(appearance); // the lists' caps follow the text scale (screens §4.1)
        }, shell.Setup, shell.Health.ModelChoice, new ViewModels.Sandbox.SandboxSettingsViewModel(shell.Health.Sandbox), new PickLines(shell.Health.LabelChoice, shell.Health.SurfaceChoice));
        try
        {
            await settings.Load(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The defaults stay; a later change in Settings tries the file again.
        }

        return settings;
    }

    /// <summary>The platform wiring's hook: the shell window is shown and its view model about to follow.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    /// <param name="window">The shell window.</param>
    /// <param name="shell">Its view model (a deep link from the single instance navigates it).</param>
    partial void ShellOpened(IClassicDesktopStyleApplicationLifetime desktop, ShellWindow window, ShellViewModel shell);
}
