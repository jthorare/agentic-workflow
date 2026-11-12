using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The sheet part (#3234): first focus inside on open, a Tab that stays inside, Esc that closes, an error line with its code under Details, and a danger primary button that reads at AA.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SheetTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SheetTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, Sheet Sheet, TextBox Field, Button First, Button Last, Button Outside) Show(bool withField = true)
    {
        var field = new TextBox { Name = "Field" };
        var first = new Button { Name = "First", Content = "Add" };
        var last = new Button { Name = "Last", Content = "Cancel" };
        var outside = new Button { Name = "Outside", Content = "Behind the sheet" };
        var sheet = new Sheet { Heading = "Add a step", Body = withField ? field : new TextBlock { Text = "Pick one." }, Buttons = [first, last] };
        var window = new Window { Width = 640, Height = 480, Content = new StackPanel { Children = { outside, sheet } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        sheet.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        return (window, sheet, field, first, last, outside);
    }

    private static bool Inside(Sheet sheet, IInputElement? focused) => focused is Visual v && (ReferenceEquals(v, sheet) || sheet.IsVisualAncestorOf(v));

    [Fact]
    public async Task First_focus_lands_on_the_first_field_in_the_body_when_the_sheet_opens() => await OnUi(() =>
    {
        var (window, sheet, field, _, _, _) = Show();

        var focused = window.FocusManager?.GetFocusedElement();

        Assert.Same(field, focused);
        Assert.True(Inside(sheet, focused));
        window.Close();
        return true;
    });

    [Fact]
    public async Task With_no_field_in_the_body_first_focus_lands_on_the_first_button() => await OnUi(() =>
    {
        var (window, _, _, first, _, _) = Show(withField: false);

        Assert.Same(first, window.FocusManager?.GetFocusedElement());
        window.Close();
        return true;
    });

    [Fact]
    public async Task Tab_from_the_last_button_wraps_to_the_first_focusable_inside_and_never_leaves() => await OnUi(() =>
    {
        var (window, sheet, field, _, last, outside) = Show();
        _ = last.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        var forward = window.FocusManager?.GetFocusedElement();

        _ = field.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        var backward = window.FocusManager?.GetFocusedElement();

        Assert.Same(field, forward);
        Assert.Same(last, backward);
        Assert.NotSame(outside, forward);
        Assert.True(Inside(sheet, backward));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Esc_closes_the_sheet_raises_Closed_and_hands_focus_back_to_what_had_it() => await OnUi(() =>
    {
        var field = new TextBox();
        var outside = new Button { Content = "Behind" };
        var sheet = new Sheet { Heading = "Remove step", Body = field, Buttons = [new Button { Content = "Keep" }] };
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { outside, sheet } } };
        window.Show();
        _ = outside.Focus(NavigationMethod.Tab);
        var closed = 0;
        sheet.Closed += (_, _) => closed++;
        sheet.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(sheet.IsOpen);
        Assert.Equal(1, closed);
        Assert.Same(outside, window.FocusManager?.GetFocusedElement());
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_error_line_shows_the_sentence_and_keeps_the_code_behind_Details_until_asked_for() => await OnUi(() =>
    {
        var (window, sheet, _, _, _, _) = Show();
        var hiddenBefore = !sheet.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == "Couldn't create the issue. The line stays as you wrote it.");

        sheet.ErrorText = "Couldn't create the issue. The line stays as you wrote it.";
        sheet.ErrorCode = "SND-NOTE-015";
        Dispatcher.UIThread.RunJobs();
        var texts = sheet.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        var details = sheet.GetVisualDescendants().OfType<Expander>().Single();

        Assert.True(hiddenBefore);
        Assert.Contains("Couldn't create the issue. The line stays as you wrote it.", texts);
        Assert.False(details.IsExpanded);
        Assert.DoesNotContain("SND-NOTE-015", texts);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_danger_primary_button_is_filled_with_ErrorFill_and_white_text() => await OnUi(() =>
    {
        var danger = new Button { Content = "Remove step" };
        danger.Classes.Add("command-control");
        danger.Classes.Add("destructive");
        danger.Classes.Add("primary");
        var window = new Window { Content = danger };
        window.Show();
        window.UpdateLayout();

        var presenter = danger.GetVisualDescendants().OfType<ContentPresenter>().First();

        Assert.Equal(Color.Parse("#c93c3c"), Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
        Assert.Equal(Color.Parse("#ffffff"), Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Foreground).Color);
        window.Close();
        return true;
    });
}
