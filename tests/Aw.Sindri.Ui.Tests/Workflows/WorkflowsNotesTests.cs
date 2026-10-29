using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Setup;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Setup;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The notes left on the Workflows page after the first pass (#1073): the Updated column moves with the clock, the decision sheet never headlines a raw id, Settings opens on its title, and the type filter has no hidden text box.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsNotesTests : FrameHost
{
    private const string DecisionId = "DEC-7";

    private static NeedsYouRowViewModel DecisionRow(string question, TimeProvider clock) =>
        new(
            new NeedsYouEntry("row-dec-7", NeedsYouUrgency.Pulse)
            {
                Kind = NeedsYouKinds.Decision,
                Title = "",
                Question = question,
                Subject = new NeedsYouSubject(NeedsYouKinds.Decision, DecisionId),
            },
            seen: true,
            reducedMotion: false,
            arrived: false,
            clock);

    private static DecisionSheetViewModel Sheet(FakeTimeProvider clock) =>
        new(new FakeSindriApi(), () => null, clock, new DecisionSender(new FakeSindriApi()));

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Updated_column_counts_on_with_the_clock()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var served = FixtureInstances.List("open").Instances[0] with { Since = clock.GetUtcNow() };
        var row = new InstanceRowViewModel(served, new FakeClipboard(), clock);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        var before = row.UpdatedText;

        clock.Advance(TimeSpan.FromMinutes(25));
        row.Tick();

        Assert.Contains(nameof(InstanceRowViewModel.UpdatedText), raised, StringComparer.Ordinal);
        Assert.Equal(Labels.Age(TimeSpan.FromMinutes(25)), row.UpdatedText, StringComparer.Ordinal);
        Assert.NotEqual(before, row.UpdatedText, StringComparer.Ordinal);
    }

    [Fact]
    public void Decision_sheet_never_headlines_its_raw_id()
    {
        var clock = new FakeTimeProvider(A.Noon);

        var asked = Sheet(clock);
        asked.Show(DecisionRow("Ship the migration tonight?", clock));
        var unasked = Sheet(clock);
        unasked.Show(DecisionRow("", clock));

        Assert.Equal("Ship the migration tonight?", asked.Heading, StringComparer.Ordinal);
        Assert.NotEmpty(unasked.Heading);
        Assert.DoesNotContain(DecisionId, unasked.Heading, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cleared_line_never_names_the_raw_id()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var sheet = Sheet(clock);
        sheet.Show(DecisionRow("Ship the migration tonight?", clock));

        await sheet.Clear();

        Assert.True(sheet.IsCleared);
        Assert.NotEmpty(sheet.ClearedLine);
        Assert.DoesNotContain(DecisionId, sheet.ClearedLine, StringComparison.Ordinal);
    }

    [Fact]
    public Task Settings_opens_with_focus_on_its_title() => OnUi(async () =>
    {
        Server.SetupAnswer = SetupFixture.Answer("extra-priority-mix");
        var (window, shell) = await Connected(s => s.Setup.Steps.Count > 0);

        Press(window, Key.G);
        Press(window, Key.U);
        Settle(window);
        await Task.Delay(50, Token);
        Settle(window);

        Assert.Equal(PageId.Settings, shell.CurrentPage);
        var title = Named<TextBlock>(window, "TitleText");
        var focused = window.FocusManager?.GetFocusedElement() as Control;
        var landed = focused is null ? "nothing" : $"{focused.GetType().Name} '{focused.Name}'";
        var inChecklist = window.GetVisualDescendants().OfType<SetupChecklist>().Any(c => focused is Visual f && c.IsVisualAncestorOf(f));
        Assert.False(inChecklist, $"focus landed inside the setup checklist: {landed}");
        Assert.True(focused is not null && (ReferenceEquals(focused, title) || (focused.IsVisualAncestorOf(title) && window.GetVisualDescendants().OfType<SettingsView>().Any(v => v.IsVisualAncestorOf(focused)))),
            $"focus should be on the Settings title, but is on {landed}");
    });

    [Fact]
    public Task Type_filter_has_no_hidden_text_box() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);
        Settle(window);

        var combo = Named<ComboBox>(window, "RecipeFilter");
        Assert.False(combo.IsEditable);
        var boxes = combo.GetVisualDescendants().OfType<TextBox>().ToList();
        var found = string.Join("; ", boxes.Select(b => $"{b.Name ?? "(unnamed)"} visible={b.IsEffectivelyVisible} focusable={b.Focusable} hit={b.IsHitTestVisible} control={ControlAutomationPeer.CreatePeerForElement(b).IsControlElement()}"));

        Assert.All(boxes, b =>
        {
            Assert.False(b.IsEffectivelyVisible, $"a TextBox in the type filter is visible: {found}");
            Assert.False(b.Focusable && b.IsEffectivelyEnabled, $"a TextBox in the type filter can take focus: {found}");
            Assert.False(b.IsHitTestVisible, $"a TextBox in the type filter takes the pointer: {found}");
            Assert.False(ControlAutomationPeer.CreatePeerForElement(b).IsControlElement(), $"a TextBox in the type filter is in the control view: {found}");
        });

        var before = shell.Workflows.RecipeFilter;
        var choice = shell.Workflows.RecipeChoices.First(c => !string.Equals(c, Labels.AllRecipes, StringComparison.Ordinal));
        foreach (var box in boxes)
        {
            box.Text = choice;
        }

        Settle(window);
        Assert.Equal(before, shell.Workflows.RecipeFilter, StringComparer.Ordinal);
    });
}
