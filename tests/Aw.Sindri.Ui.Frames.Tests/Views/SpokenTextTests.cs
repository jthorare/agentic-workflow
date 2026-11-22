using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The spoken-text part (#3234): one Text for a screen reader, named by what it shows (or by its own words), with the inner TextBlock Raw so nothing is read twice.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SpokenTextTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SpokenTextTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, SpokenText Line) Show(Action<SpokenText> set)
    {
        var line = new SpokenText();
        set(line);
        var window = new Window { Width = 300, Height = 200, Content = line };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, line);
    }

    private static TextBlock Inner(SpokenText line) => line.GetVisualDescendants().OfType<TextBlock>().Single();

    [Fact]
    public async Task It_draws_its_text_in_one_TextBlock_and_the_screen_reader_name_is_that_text() => await OnUi(() =>
    {
        var (window, line) = Show(l => l.Text = "Saved 12 s ago");

        Assert.Equal("Saved 12 s ago", Inner(line).Text);
        Assert.Equal("Saved 12 s ago", AutomationProperties.GetName(line));
        window.Close();
        return true;
    });

    [Fact]
    public async Task It_is_a_Text_control_for_the_reader_and_the_inner_TextBlock_is_Raw() => await OnUi(() =>
    {
        var (window, line) = Show(l => l.Text = "Nothing to show yet");

        Assert.Equal(AutomationControlType.Text, ControlAutomationPeer.CreatePeerForElement(line).GetAutomationControlType());
        Assert.Equal(AccessibilityView.Control, AutomationProperties.GetAccessibilityView(line));
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(Inner(line)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Spoken_wins_over_the_visible_text_and_a_change_of_text_updates_the_name() => await OnUi(() =>
    {
        var (window, line) = Show(l => { l.Text = "3 of 4"; l.Spoken = "3 of 4 areas searched"; });
        var first = AutomationProperties.GetName(line);

        line.Spoken = null;
        line.Text = "4 of 4";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("3 of 4 areas searched", first);
        Assert.Equal("4 of 4", AutomationProperties.GetName(line));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Empty_text_hides_the_line_and_gives_it_no_name() => await OnUi(() =>
    {
        var (window, line) = Show(l => l.Text = "Visible");

        line.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Assert.False(line.IsVisible);
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(line)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task TextClasses_style_the_inner_text_and_Wraps_wraps_it() => await OnUi(() =>
    {
        var (window, line) = Show(l => { l.Text = "A long sentence that needs to wrap inside a narrow column of the page"; l.TextClasses = "caption subtle"; l.Wraps = true; });
        var inner = Inner(line);

        Assert.Contains("caption", inner.Classes, StringComparer.Ordinal);
        Assert.Contains("subtle", inner.Classes, StringComparer.Ordinal);
        Assert.Equal(TextWrapping.Wrap, inner.TextWrapping);
        window.Close();
        return true;
    });
}
