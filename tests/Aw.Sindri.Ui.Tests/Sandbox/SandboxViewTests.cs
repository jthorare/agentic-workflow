using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Sandbox;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Icons;
using Aw.Sindri.Ui.Views.Sandbox;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>
/// The sandbox views under the headless platform, one test per accessibility.json row this part owns: Check again, and the
/// Health row and Settings section (sandbox spec 8 Accessibility). Live regions are zero-width, in the tree before their state, and never "".
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SandboxViewTests
{
    private readonly SandboxTestBed _bed = new();
    private readonly List<Window> _windows = [];

    private static CancellationToken Token => SandboxTestBed.Token;

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SandboxViewTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private Window Show(Control content)
    {
        var window = new Window { Width = 900, Height = 700, Content = content };
        _windows.Add(window);
        window.Show();
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string name)
        where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    private static double Top(Window window, Control control) => control.TranslatePoint(new Point(0, 0), window)!.Value.Y;

    private static void Click(Window window, string name)
    {
        Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
    }

    private async Task<Window> ShowCheck(string[] blockers, string[] titles)
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(blockers, titles);
        var window = Show(new SandboxCheckPanel { DataContext = _bed.Check });
        await Task.CompletedTask;
        return window;
    }

    // Check again: the live region is zero-width and in the tree before its state, its Text is never "", and it is Polite while it speaks.
    [Fact]
    public Task Check_again_has_a_zero_width_live_region_in_the_tree_before_its_state_that_is_never_empty() => OnUi(async () =>
    {
        var window = await ShowCheck(["No sandbox tool."], []);
        var live = Find<TextBlock>(window, "SandboxCheckLive");

        Assert.Equal((0d, 0d, true, "Check again"), (live.Bounds.Width, live.Opacity, live.IsVisible, live.Text));

        await _bed.Check.Check(Token);
        Settle(window);

        Assert.Equal(("Still blocked: 1 thing to fix", AutomationLiveSetting.Polite, 0d), (live.Text, AutomationProperties.GetLiveSetting(live), live.Bounds.Width));
    });

    [Fact]
    public Task Check_again_keeps_its_label_and_focus_while_checking_and_a_second_click_does_nothing() => OnUi(async () =>
    {
        _bed.Server.SandboxCheckGate = new TaskCompletionSource();
        var window = await ShowCheck(["No sandbox tool."], []);
        var button = Find<Button>(window, "SandboxCheckButton");
        _ = button.Focus();

        Click(window, "SandboxCheckButton");
        Click(window, "SandboxCheckButton");

        Assert.Equal((1, true, true, "Check again"), (_bed.Server.SandboxCheckCalls, button.IsEnabled, ReferenceEquals(window.FocusManager?.GetFocusedElement(), button), AutomationProperties.GetName(button)));
        Assert.True(Find<Control>(window, "SandboxCheckSpinner").IsEffectivelyVisible);
        _bed.Server.SandboxCheckGate.SetResult();
        Settle(window);
    });

    [Fact]
    public Task Check_again_shows_a_check_that_did_not_run_as_one_caption_under_the_blockers_and_none_when_empty() => OnUi(async () =>
    {
        var window = await ShowCheck(["No sandbox tool."], []);
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool."], [], notChecked: [SandboxTestBed.HooksNotChecked]);
        await _bed.Check.Check(Token);
        Settle(window);

        var list = Find<ItemsControl>(window, "SandboxCheckNotChecked");
        var caption = list.GetLogicalDescendants().OfType<TextBlock>().Single();
        Assert.Equal((true, SandboxTestBed.HooksNotChecked, SandboxTestBed.HooksNotChecked), (list.IsEffectivelyVisible, caption.Text, AutomationProperties.GetName(caption)));
        Assert.True(Top(window, Find<ItemsControl>(window, "SandboxBlockers")) < Top(window, list));

        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool."], []);
        await _bed.Check.Check(Token);
        Settle(window);
        Assert.False(list.IsEffectivelyVisible);
    });

    // Health row: the name is "Sandbox: <state>" and wraps; the state is also a word and a mark.
    [Fact]
    public Task The_health_row_is_named_sandbox_and_its_state_wraps_with_the_state_as_a_word() => OnUi(async () =>
    {
        _bed.Server.SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "blocked");
        await _bed.Card.Refresh(SandboxTestBed.Api, Token);
        var window = Show(new SandboxHealthCard { DataContext = _bed.Card });

        var row = Find<TextBlock>(window, "SandboxRow");
        Assert.Equal(("Sandbox: Sandboxed sessions can't start: bubblewrap isn't installed.", Avalonia.Media.TextWrapping.Wrap, "blocked"), (AutomationProperties.GetName(row), row.TextWrapping, Find<TextBlock>(window, "SandboxWord").Text));
        Assert.True(Find<Control>(window, "SandboxCheckSlot").IsEffectivelyVisible);

        _bed.Server.SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "ready");
        await _bed.Card.Refresh(SandboxTestBed.Api, Token);
        Settle(window);
        Assert.Equal(("Sandbox: ready", false), (row.Text, Find<Control>(window, "SandboxCheckSlot").IsEffectivelyVisible));
    });

    // #3494: every state is a word and a mark (sandbox spec 8; UI spec 11.3). The never-checked card had the word and no mark.
    [Fact]
    public Task The_never_checked_card_has_a_state_mark_before_its_word_on_the_same_centre_line_8_px_apart() => OnUi(async () =>
    {
        _bed.Server.SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "never-checked");
        await _bed.Card.Refresh(SandboxTestBed.Api, Token);
        // The card lives in the shell window, whose ShellStyles give a StateIcon its Size.Icon; a bare Window would leave the mark 0 x 0.
        var card = new SandboxHealthCard { DataContext = _bed.Card };
        card.Styles.Add(new StyleInclude(new Uri("avares://Aw.Sindri.Ui/")) { Source = new Uri("avares://Aw.Sindri.Ui/Views/Shell/ShellStyles.axaml") });
        var window = Show(card);

        var mark = Find<StateIcon>(window, "SandboxGlyph");
        var word = Find<TextBlock>(window, "SandboxWord");
        var markBox = new Rect(mark.TranslatePoint(default, window) ?? default, mark.Bounds.Size);
        var wordBox = new Rect(word.TranslatePoint(default, window) ?? default, word.Bounds.Size);

        Assert.Equal(("not checked", StateGlyph.Stale, true), (word.Text, mark.Glyph, mark.IsEffectivelyVisible && markBox.Width > 0 && markBox.Height > 0));
        Assert.Equal(8, wordBox.X - markBox.Right, 0.5);
        Assert.Equal(markBox.Center.Y, wordBox.Center.Y, 1.0);   // within 1 px (a tolerance, not decimal places)
    });

    [Fact]
    public Task The_health_row_is_dimmed_while_sindri_does_not_answer() => OnUi(async () =>
    {
        _bed.Server.GoDown();
        await _bed.Card.Refresh(SandboxTestBed.Api, Token);
        var window = Show(new SandboxHealthCard { DataContext = _bed.Card });

        Assert.Equal(("Sandbox: unknown", 0.5d), (Find<TextBlock>(window, "SandboxRow").Text, Find<Border>(window, "SandboxPanel").Opacity));
    });

    // Settings section: in the page's tab order, its heading carries the state, dimmed while serve is down.
    [Fact]
    public Task The_settings_section_shows_the_fields_when_loaded_and_is_dimmed_when_serve_is_down() => OnUi(async () =>
    {
        _bed.Server.SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "learning");
        var section = new SandboxSettingsViewModel(_bed.Card);
        await _bed.Card.Refresh(SandboxTestBed.Api, Token);
        using var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _ => { }, setup: null, modelChoice: null, sandbox: section);
        var window = Show(new SettingsView { DataContext = settings });

        Assert.Equal(("Sandbox: learning, ends in 3 days", true), (Find<TextBlock>(window, "SandboxTitle").Text, Find<Border>(window, "SandboxFieldsCard").IsEffectivelyVisible));

        _bed.Server.GoDown();
        await _bed.Card.Refresh(SandboxTestBed.Api, Token);
        Settle(window);
        Assert.Equal(("Sandbox: status unavailable", 0.5d, false), (Find<TextBlock>(window, "SandboxTitle").Text, Find<StackPanel>(window, "SandboxSection").Opacity, Find<Border>(window, "SandboxFieldsCard").IsEffectivelyVisible));
    });
}
