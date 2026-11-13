using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The list picker part (#3234): grouped rows, first focus for the host, arrow keys across groups, Enter picks, Esc closes, a disabled row says why, the error line keeps its code behind Details.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ListPickerTests
{
    private static readonly PickerGroup[] _groups =
    [
        new("Triggers", [new PickerItem("schedule", "On a schedule"), new PickerItem("webhook", "When a webhook arrives", IsEnabled: false, Reason: "Needs a connection")]),
        new("Steps", [new PickerItem("agent", "Run an agent"), new PickerItem("code", "Run code"), new PickerItem("ask", "Ask me")]),
    ];

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ListPickerTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, ListPicker Picker) Show()
    {
        var picker = new ListPicker { Heading = "Add a step", Groups = _groups, CancelText = "Cancel" };
        var window = new Window { Width = 520, Height = 520, Content = picker };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, picker);
    }

    private static List<SelectableTile> Tiles(ListPicker picker) => [.. picker.GetVisualDescendants().OfType<SelectableTile>()];

    private static string? Name(IInputElement? element) => (element as SelectableTile)?.Content as string;

    [Fact]
    public async Task There_is_a_heading_a_header_per_group_and_a_row_per_choice() => await OnUi(() =>
    {
        var (window, picker) = Show();

        var texts = picker.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        Assert.Contains("Add a step", texts, StringComparer.Ordinal);
        Assert.Contains("Triggers", texts, StringComparer.Ordinal);
        Assert.Contains("Steps", texts, StringComparer.Ordinal);
        Assert.Equal(["On a schedule", "When a webhook arrives", "Run an agent", "Run code", "Ask me"], Tiles(picker).Select(t => (string?)t.Content).ToList());
        Assert.All(Tiles(picker), t => Assert.Contains("row", t.Classes, StringComparer.Ordinal));
        window.Close();
        return true;
    });

    [Fact]
    public async Task FocusFirst_lands_on_the_first_enabled_row() => await OnUi(() =>
    {
        var (window, picker) = Show();

        var took = picker.FocusFirst();
        Dispatcher.UIThread.RunJobs();

        Assert.True(took);
        Assert.Equal("On a schedule", Name(window.FocusManager?.GetFocusedElement()));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Down_and_Up_move_across_groups_skip_a_disabled_row_and_stop_at_the_ends() => await OnUi(() =>
    {
        var (window, picker) = Show();
        _ = picker.FocusFirst();
        var seen = new List<string?> { Name(window.FocusManager?.GetFocusedElement()) };

        foreach (var _ in Enumerable.Range(0, 4))
        {
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            seen.Add(Name(window.FocusManager?.GetFocusedElement()));
        }

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        var back = Name(window.FocusManager?.GetFocusedElement());

        Assert.Equal(["On a schedule", "Run an agent", "Run code", "Ask me", "Ask me"], seen);
        Assert.Equal("Run code", back);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Home_and_End_jump_to_the_first_and_last_enabled_row() => await OnUi(() =>
    {
        var (window, picker) = Show();
        _ = picker.FocusFirst();

        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        var last = Name(window.FocusManager?.GetFocusedElement());
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        var first = Name(window.FocusManager?.GetFocusedElement());

        Assert.Equal("Ask me", last);
        Assert.Equal("On a schedule", first);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Enter_on_a_row_raises_Picked_with_its_item_and_Esc_raises_Closed() => await OnUi(() =>
    {
        var (window, picker) = Show();
        var picked = new List<PickerItem>();
        var closed = 0;
        picker.Picked += (_, e) => picked.Add(e.Value);
        picker.Closed += (_, _) => closed++;
        _ = picker.FocusFirst();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.Equal(["agent"], picked.Select(i => i.Id).ToList());
        Assert.Equal(1, closed);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_disabled_row_is_off_shows_its_reason_and_is_never_picked() => await OnUi(() =>
    {
        var (window, picker) = Show();
        var picked = 0;
        picker.Picked += (_, _) => picked++;
        var off = Tiles(picker).Single(t => (string?)t.Content == "When a webhook arrives");

        off.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.False(off.IsEnabled);
        Assert.Equal("Needs a connection", ToolTip.GetTip(off));
        Assert.Equal(0, picked);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_chosen_row_is_drawn_selected() => await OnUi(() =>
    {
        var (window, picker) = Show();

        picker.SelectedId = "code";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Run code"], Tiles(picker).Where(t => t.IsSelected).Select(t => (string?)t.Content).ToList());
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_error_line_shows_the_sentence_and_keeps_the_code_behind_Details() => await OnUi(() =>
    {
        var (window, picker) = Show();
        const string sentence = "Couldn't add the step. The graph is unchanged.";
        var hiddenBefore = !picker.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == sentence);

        picker.ErrorText = sentence;
        picker.ErrorCode = "SND-GRAPH-014";
        Dispatcher.UIThread.RunJobs();
        var texts = picker.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        var details = picker.GetVisualDescendants().OfType<Expander>().Single();

        Assert.True(hiddenBefore);
        Assert.Contains(sentence, texts, StringComparer.Ordinal);
        Assert.False(details.IsExpanded);
        Assert.DoesNotContain("SND-GRAPH-014", texts, StringComparer.Ordinal);
        window.Close();
        return true;
    });
}
