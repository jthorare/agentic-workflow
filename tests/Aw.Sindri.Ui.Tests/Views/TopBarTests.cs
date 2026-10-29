using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Icons;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The top bar's health pill and Refresh under the headless platform (UI spec §4.1, §11.4): the "Sending…" live region,
/// the pill while Sindri isn't running, and the spinner under reduced motion switched on mid-flight.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TopBarTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TopBarTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    Motion.Apply(Application.Current!.Resources, reduce: false);
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(Func<ShellViewModel, bool> ready)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        var following = shell.Run(_stop.Token);
        await Until(() => ready(shell) || following.IsFaulted);
        Assert.False(following.IsFaulted, following.Exception?.ToString());
        Assert.True(ready(shell), "the shell never reached the expected state");
        Settle(window);
        return (window, shell);
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Stale()
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        Settle(window);
        return (window, shell);
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    // accessibility-tester-0 / code-reviewer-0 (§4.1 "sending", docs/csharp/sindri-ui.md "LiveSetting on macOS"): "Sending…"
    // is announced by a TextBlock whose Text changes (a Border's bound name raises no change), with no separate name. Its
    // text is never "" (Avalonia #22312), it is live (Polite) only while sending, and every change of its text happens
    // with the live setting that matches: to "Sending…" while Polite, back to its resting text while Off, so only
    // "Sending…" is ever spoken. It is in the tree only while a refresh is in flight, and takes no room in the bar.
    // Review run 4 (accessibility-tester-2) hid the ⌘K field from assistive tech while it did nothing. U1 opens the
    // palette from it, so the field is a Tab stop and exposed with its name; only its decorative parts (the placeholder
    // text, the keycap) stay Raw so the name is not read twice.
    // #1544: when the pill names a problem at 1280 px, the problem text is what gives way; the ⌘K field keeps its whole
    // placeholder, and the trimmed text stays readable in the pill's tooltip and accessible name.
    [Fact]
    public Task A_long_problem_text_gives_way_before_the_search_placeholder() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with
        {
            Health = new HealthView("degraded", [new HealthProblemView("SND-PROFILE-001", "the profile changed since approval", "the profile changed since approval and a much longer clause")]),
        };
        var (window, shell) = await Show(s => s.State == ShellState.Normal && s.Pill.HasProblems);
        window.Width = 1280;
        Settle(window);

        var placeholder = Named<TextBlock>(window, "SearchText");
        var problems = Named<TextBlock>(window, "PillProblems");
        using var layout = new TextLayout(placeholder.Text, new Typeface(placeholder.FontFamily, placeholder.FontStyle, placeholder.FontWeight), placeholder.FontSize, foreground: null);
        var natural = layout.Width;
        Assert.True(
            placeholder.Bounds.Width >= natural + TopBar.PlaceholderSlack - 0.5,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the placeholder got {placeholder.Bounds.Width} of its {natural}"));
        Assert.True(problems.IsEffectivelyVisible);
        Assert.Equal(shell.Pill.Text, ToolTip.GetTip(Named<Button>(window, "HealthPill")));
        Assert.Equal(shell.Pill.Text, AutomationProperties.GetName(Named<Button>(window, "HealthPill")));
    });

    [Fact]
    public Task The_search_field_is_exposed_to_assistive_tech_and_reachable_by_keyboard() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);
        var field = window.Get<TopBar>("TopBar").Get<Control>("SearchField");
        Assert.True(field.IsEffectivelyVisible);
        Assert.True(field.Focusable);
        Assert.NotEqual(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(field));
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(field)));
    });

    [Fact]
    public Task Sending_is_announced_by_a_polite_TextBlock_that_is_never_emptied() => OnUi(async () =>
    {
        var (window, shell) = await Stale();
        var live = Named<TextBlock>(window, "RefreshSending");
        var changes = new List<(string? Text, AutomationLiveSetting Live)>();
        live.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
            {
                changes.Add((live.Text, AutomationProperties.GetLiveSetting(live)));
            }
        };
        Assert.False(live.IsEffectivelyVisible);
        Assert.False(string.IsNullOrEmpty(live.Text));
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(live)), "the live region has a separate name");

        await RefreshAndAnswer(window, shell, live);
        await RefreshAndAnswer(window, shell, live);

        Assert.All(changes, c => Assert.False(string.IsNullOrEmpty(c.Text)));
        Assert.Equal(4, changes.Count);
        Assert.All(changes, c => Assert.Equal(c.Text == Labels.Sending ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off, c.Live));
    });

    // One refresh through its in-flight phase: the live region shows, says "Sending…" after 300 ms, and hides with the
    // answer; then the views go stale again for the next one.
    private async Task RefreshAndAnswer(Window window, ShellViewModel shell, TextBlock live)
    {
        _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Named<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Assert.True(live.IsEffectivelyVisible, "the live region isn't in the tree while in flight");
        _clock.Advance(HealthViewModel.SendingAfter);
        await Until(() => shell.Health.IsSending);
        Settle(window);

        Assert.Equal((Labels.Sending, AutomationLiveSetting.Polite), (live.Text, AutomationProperties.GetLiveSetting(live)));
        Assert.Equal(0, live.Bounds.Width);

        _server.StatusGate.SetResult();
        await Until(() => shell.Health.CanRefresh);
        Settle(window);
        Assert.False(live.IsEffectivelyVisible);
        Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(live));

        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        Settle(window);
    }

    // accessibility-tester-1 (states.html "Not running": the pill is the state, not a link): while Sindri isn't running the
    // Not running card is the health state and the Health page has nothing to show, so the pill takes no click, no Enter
    // and no Tab stop (disabled, like the navigation beside it). It still says the state to assistive tech, its word keeps
    // its ink (not dimmed), and nothing changes the page or the remembered last page.
    [Fact]
    public Task While_not_running_the_health_pill_is_the_state_and_opens_nothing() => OnUi(async () =>
    {
        var (window, shell) = await Show(s => s.State == ShellState.NotRunning);
        var pill = Named<Button>(window, "HealthPill");
        var writes = _preferences.Writes;

        Assert.False(pill.IsEffectivelyEnabled);
        Assert.Equal(shell.Pill.Text, AutomationProperties.GetName(pill));
        Assert.Equal(Colour("Color.Ink"), ((ISolidColorBrush)Named<TextBlock>(window, "HealthPillText").Foreground!).Color);

        pill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.Equal(writes, _preferences.Writes);
    });

    // accessibility-tester-2 (§11.4: under reduced motion every duration is 0): a spinner already turning stops when
    // reduced motion turns on mid-refresh, and stands still in the icon's place; it turns again when motion comes back.
    [Fact]
    public Task A_turning_spinner_stops_when_reduced_motion_turns_on_mid_refresh() => OnUi(async () =>
    {
        var (window, shell) = await Stale();
        _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Named<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        var spinner = Named<StateIcon>(window, "RefreshSpinner");
        Assert.True(spinner.IsSpinning, "the spinner isn't turning");

        Motion.Apply(Application.Current!.Resources, reduce: true);
        Settle(window);
        Assert.False(spinner.IsSpinning, "the spinner kept turning under reduced motion");
        Assert.Equal(0, spinner.Angle);
        Assert.True(spinner.IsEffectivelyVisible, "the still spinner left the icon's place");

        Motion.Apply(Application.Current!.Resources, reduce: false);
        Settle(window);
        Assert.True(spinner.IsSpinning, "the spinner didn't turn again with motion back");

        _server.StatusGate.SetResult();
        await Until(() => shell.Health.CanRefresh);
        Settle(window);
        Assert.False(spinner.IsSpinning);
    });

    private static Color Colour(string key) =>
        Application.Current!.TryGetResource(key, theme: null, out var v) && v is Color c ? c : default;
}
