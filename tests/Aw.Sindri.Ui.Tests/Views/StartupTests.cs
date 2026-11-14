using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The app's real start-up path, the way the desktop head runs it: <see cref="SindriApp.Create"/> over the platform
/// ports, then the framework initialization with a classic desktop lifetime. The window shows with Settings in its
/// slot, the tray follows the shell, and the single instance's messages are consumed.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StartupTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeTray _tray = new();
    private readonly FakeInstanceChannel _channel = new();
    private readonly ClassicDesktopStyleApplicationLifetime _desktop = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _app?.Dispose();   // a listener left running reads the dispatcher in the next test's set-up
        _desktop.Dispose();
    }

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StartupTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, Token);

    private readonly RecordingLogger _log = new();
    private int? _exitCode;
    private SindriApp? _app;

    private SindriPlatform Platform(FakeSingleInstance instance) =>
        new(new InMemoryEndpointRecords(), new InMemoryHumanToken(), new InMemoryPreferencesStore(), new FakeMotionPreference(), instance, _tray) { Log = _log };

    // The app as the head starts it: created over the ports, initialized, then the framework's initialization completes.
    // The lifetime's real Shutdown also stops the dispatcher the session shares, so the test records the exit code instead.
    private SindriApp Start(FakeSingleInstance instance)
    {
        var app = SindriApp.Create(Platform(instance), _clock);
        _app = app;
        app.End = (_, exitCode) => _exitCode ??= exitCode;
        app.ApplicationLifetime = _desktop;
        app.Initialize();
        app.OnFrameworkInitializationCompleted();
        return app;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }
    }

    // Quit from the tray (the lifetime's shutdown) and close what start-up opened, so nothing is left for the next test.
    private void Quit()
    {
        _tray.ClickQuit();
        foreach (var window in _desktop.Windows)
        {
            window.Close();
        }

        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Start_up_shows_the_shell_with_settings_and_starts_the_tray_and_the_single_instance() => OnUi(async () =>
    {
        var instance = new FakeSingleInstance(_channel);
        Assert.True(await instance.TryClaim(Token));
        _ = Start(instance);
        await Until(() => _desktop.MainWindow is { IsVisible: true } && _tray.Shown is not null);

        try
        {
            var window = Assert.IsType<ShellWindow>(_desktop.MainWindow);
            Assert.True(window.IsVisible, "the shell window never showed");
            _ = Assert.IsType<SettingsView>(window.SettingsSlot.Content);
            Assert.NotNull(_tray.Shown);

            // A later launch's deep link is consumed: the running app navigates on it.
            Assert.True(await new FakeSingleInstance(_channel).Send("item/ITM-01JC8", Token));
            var shell = Assert.IsType<ShellViewModel>(window.DataContext);
            await Until(() => shell.IsSheetOpen);
            Assert.Equal("ITM-01JC8", shell.SheetSubject);
        }
        finally
        {
            Quit();
            await instance.DisposeAsync();
        }
    });

    // The real start path: a palette Settings entry run through the app's shell must reach the Settings page's filter (#3362). Deleting the SindriApp line that joins them turns it red.
    private async Task RunTextSizeEntry(Func<ShellViewModel, SettingsViewModel, Task>? before = null)
    {
        var instance = new FakeSingleInstance(_channel);
        Assert.True(await instance.TryClaim(Token));
        _ = Start(instance);
        await Until(() => _desktop.MainWindow is { IsVisible: true } && _tray.Shown is not null);

        try
        {
            var window = Assert.IsType<ShellWindow>(_desktop.MainWindow);
            var shell = Assert.IsType<ShellViewModel>(window.DataContext);
            var settings = Assert.IsType<SettingsViewModel>(Assert.IsType<SettingsView>(window.SettingsSlot.Content).DataContext);
            if (before is not null)
            {
                await before(shell, settings);
            }

            shell.Palette.Open();
            await shell.Palette.Run(shell.Palette.Entries.Single(static e => string.Equals(e.Id, "setting:TextSize", StringComparison.Ordinal)), Token);

            Assert.Equal(PageId.Settings, shell.CurrentPage);
            Assert.Equal("text size", settings.Filter);
        }
        finally
        {
            Quit();
            await instance.DisposeAsync();
        }
    }

    [Fact]
    public Task A_palette_Text_size_entry_shows_Settings_filtered_through_the_real_start_up() => OnUi(() => RunTextSizeEntry());

    [Fact]
    public Task A_palette_Text_size_entry_with_Settings_already_open_still_sets_the_filter() => OnUi(() => RunTextSizeEntry((shell, _) =>
    {
        shell.GoTo(PageId.Settings);
        return Task.CompletedTask;
    }));

    [Fact]
    public Task A_palette_Text_size_entry_replaces_a_filter_that_already_has_text() => OnUi(() => RunTextSizeEntry((_, settings) =>
    {
        settings.Filter = "linear";
        return Task.CompletedTask;
    }));

    // A start-up that throws is never silent: the app logs the code and the exception's type (no message, no stack),
    // and ends with exit code 2 instead of lingering without a window or tray.
    [Fact]
    public Task A_failed_start_up_logs_its_code_and_ends_the_app_with_exit_code_2() => OnUi(async () =>
    {
        var instance = new FakeSingleInstance(_channel);
        Assert.True(await instance.TryClaim(Token));
        _tray.FailNextShow(new InvalidOperationException("no status bar at /secret/path"));

        try
        {
            _ = Start(instance);
            await Until(() => _exitCode is not null);

            Assert.Equal(2, _exitCode);
            var entry = Assert.Single(_log.Entries, e => e.Level == LogLevel.Error);
            Assert.Equal("SND-UI-004", entry.EventName);
            Assert.Contains(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("/secret/path", entry.Message, StringComparison.Ordinal);
            Assert.Null(entry.Exception);
        }
        finally
        {
            foreach (var window in _desktop.Windows.ToList())
            {
                window.Close();
            }

            Dispatcher.UIThread.RunJobs();
            await instance.DisposeAsync();
        }
    });
}
