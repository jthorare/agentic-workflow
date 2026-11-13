using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The peek pane's head and body at render scaling 1 and 1.3 (#3538): the head is 48 tall with 24 px icons and 8 px gaps, and a long plain-text body wraps instead of cutting mid-word.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class PeekScaleTests
{
    private const string LongSentence = "The reviewer asked for the retry budget to move out of the worker and into the ledger, so a resend after ten seconds hits the same row and never inserts twice, which also means the dashboard counts one attempt per request and not one per pod that saw it first.";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PeekScaleTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    // No product view fills PeekPane.Body yet (grep of src/Sindri/Aw.Sindri.Ui, 2026-11-13); the callers are tests: RowGrammarTests passes a bare TextBlock, so that is the shape here, and a plain string is the other one a caller can pass.
    private static (Window Window, PeekPane Peek, TextBlock Body) Show(double scaling, bool asString = false)
    {
        var textBlock = new TextBlock { Text = LongSentence };   // no wrapping set
        var peek = new PeekPane { Title = "A title", ItemId = "r-1", Fields = [new PeekField("Owner", "Maya")], Body = asString ? LongSentence : textBlock, Actions = new Button { Content = "Approve" } };
        var window = new Window { Content = peek, Width = 500, Height = 600 };
        Assert.Null(RowFlatRedTests.SetHostScaling(window, scaling));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var body = asString ? peek.GetVisualDescendants().OfType<TextBlock>().First(t => string.Equals(t.Text, LongSentence, StringComparison.Ordinal)) : textBlock;
        return (window, peek, body);
    }

    // One device pixel in DIPs: at 1.3 a 24 DIP icon is 31.2 px, layout rounds it to 32 px and it reads back as 24.6 DIP.
    private static double Tolerance(double scaling) => Math.Max(0.5, 1 / scaling);

    private static T Part<T>(PeekPane peek, string name)
        where T : Control => peek.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public async Task The_peek_head_is_48_px_tall(double scaling) => await OnUi(() =>
    {
        var (window, peek, _) = Show(scaling);

        Assert.Equal(48, Part<Border>(peek, "PART_Head").Bounds.Height, Tolerance(scaling));
        window.Close();
        return true;
    });

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public async Task The_peek_icons_are_24_px_squares_with_8_px_gaps(double scaling) => await OnUi(() =>
    {
        var (window, peek, _) = Show(scaling);
        var open = Part<Button>(peek, "PART_OpenFull");
        var close = Part<Button>(peek, "PART_Close");
        var title = Part<TextBlock>(peek, "PART_Title");
        var id = Part<TextBlock>(peek, "PART_Id");

        Assert.Equal(24, open.Bounds.Width, Tolerance(scaling));
        Assert.Equal(24, open.Bounds.Height, Tolerance(scaling));
        Assert.Equal(24, close.Bounds.Width, Tolerance(scaling));
        Assert.Equal(24, close.Bounds.Height, Tolerance(scaling));
        Assert.Equal(8, close.Bounds.X - open.Bounds.Right, Tolerance(scaling));
        Assert.Equal(8, id.Bounds.X - title.Bounds.Right, Tolerance(scaling));
        Assert.True(open.Bounds.X - id.Bounds.Right >= 8 - Tolerance(scaling), "the id sits 8 px or more from the first icon");
        window.Close();
        return true;
    });

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(1.3, false)]
    [InlineData(1.0, true)]
    [InlineData(1.3, true)]
    public async Task A_long_plain_text_body_wraps_inside_the_body_area(double scaling, bool asString) => await OnUi(() =>
    {
        var (window, peek, body) = Show(scaling, asString);
        var area = Part<ContentPresenter>(peek, "PART_Body").Bounds.Width;

        Assert.Equal(TextWrapping.Wrap, body.TextWrapping);
        Assert.True(body.Bounds.Width <= area + 0.5, string.Create(CultureInfo.InvariantCulture, $"the body is {body.Bounds.Width} wide in an area of {area}"));
        Assert.True(body.Bounds.Height > body.FontSize * 2, string.Create(CultureInfo.InvariantCulture, $"the body is {body.Bounds.Height} tall: one line, so it was cut"));
        window.Close();
        return true;
    });

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public async Task Body_lines_sit_20_px_apart(double scaling) => await OnUi(() =>
    {
        var (window, _, body) = Show(scaling);
        var lineCount = Math.Round(body.Bounds.Height / 20, MidpointRounding.AwayFromZero);

        Assert.Equal(20, body.LineHeight, 0.01);
        Assert.True(lineCount >= 2, "the body wraps to at least two lines");
        Assert.Equal(lineCount * 20, body.Bounds.Height, Tolerance(scaling));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Body_text_is_ink_muted() => await OnUi(() =>
    {
        var (window, _, body) = Show(1.0);

        var brush = Assert.IsType<ISolidColorBrush>(body.Foreground, exactMatch: false);
        Assert.Equal(Color.Parse("#d0d6e0"), brush.Color);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_textblock_nested_in_a_structured_body_keeps_nowrap() => await OnUi(() =>
    {
        var nested = new TextBlock { Text = LongSentence };
        var peek = new PeekPane { Title = "A title", ItemId = "r-1", Body = new StackPanel { Children = { nested } } };
        var window = new Window { Content = peek, Width = 500, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(TextWrapping.NoWrap, nested.TextWrapping);
        window.Close();
        return true;
    });
}
