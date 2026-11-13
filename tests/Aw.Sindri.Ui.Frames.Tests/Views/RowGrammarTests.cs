using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The measured spec of the row grammar (#2978, qa HOLD on #3189): the actions strip, the glyph size, the peek pane's padding and hairlines, the 8 px gaps and the sticky copy's fill.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowGrammarTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowGrammarTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static (Window Window, RowList List) Show(IEnumerable<GalleryGroup> groups, double height = 600)
    {
        var list = new RowList { ItemsSource = groups.ToList(), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1440, Height = height };
        window.Show();
        Settle(window);
        return (window, list);
    }

    private static Color Brush(string key) => Application.Current!.FindResource(key) is ISolidColorBrush b ? b.Color : default;

    private static double Top(Visual v, Visual root) => v.TranslatePoint(default, root)?.Y ?? double.NaN;

    private static List<RowGroupHeader> Headers(RowList list) => [.. list.GetVisualDescendants().OfType<RowGroupHeader>().Where(h => h.Group is not null && h.Name != "PART_Sticky" && h.Bounds.Height > 0).OrderBy(h => Top(h, list))];

    private sealed class Nop : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    [Fact]
    public async Task The_actions_strip_has_a_Surface2_fill_and_8_px_of_padding_so_it_covers_the_age_and_waits_on_text() => await OnUi(() =>
    {
        var groups = new List<GalleryGroup> { RowGallery.Group("Needs you", 4, 0, i => RowGallery.Row(i, action: new Nop())) };
        var (window, list) = Show(groups);
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
        Assert.True(row.Focus(NavigationMethod.Directional));
        Settle(window);

        var strip = Assert.IsType<RowActionsStrip>(Assert.Single(row.GetVisualChildren()));   // the row's only child, and only while it is hot

        Assert.True(strip.IsVisible);
        Assert.Equal(row.Bounds.Width - 24, strip.Bounds.Right, 0.5);   // 24 px from the right edge, over the age and waits-on text
        Assert.True(strip.Bounds.X <= row.Layout.AgeRect.X, string.Create(CultureInfo.InvariantCulture, $"strip starts at {strip.Bounds.X}, the age at {row.Layout.AgeRect.X}"));   // it covers the age and the waits-on column whole
        Assert.True(strip.Bounds.Width >= row.Layout.AgeRect.Width + 12 + row.Layout.WaitRect.Width, string.Create(CultureInfo.InvariantCulture, $"strip is {strip.Bounds.Width} wide"));   // grammar.css: min-width col-age + col-wait + sp3 (44 + 112 + 12 = 168)
        Assert.Equal(Brush("Brush.Surface2"), Assert.IsType<ISolidColorBrush>(strip.Background, exactMatch: false).Color);
        Assert.Equal(8, strip.Padding.Left, 0.01);
        var first = strip.GetVisualDescendants().OfType<Button>().First();
        Assert.Equal(8, first.TranslatePoint(default, strip)?.X ?? double.NaN, 0.5);   // grammar.css: padding-left 8 and the 4 px gap only between buttons, so the first button starts at 8
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_state_glyph_ring_is_14_px_outside_to_outside() => await OnUi(() =>
    {
        var radius = (double)typeof(RowGlyphPainter).GetField("Radius", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        var stroke = (double)typeof(RowGlyphPainter).GetField("Stroke", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;

        Assert.Equal(14, 2 * (radius + (stroke / 2)), 0.01);
        return true;
    });

    [Fact]
    public async Task The_peek_pane_pads_its_head_fields_body_and_actions_as_the_grammar_says_and_draws_three_hairlines() => await OnUi(() =>
    {
        var peek = new PeekPane { Title = "A title", ItemId = "r-1", Fields = [new PeekField("Owner", "Maya"), new PeekField("Branch", "main")], Body = new TextBlock { Text = "Body" }, Actions = new Button { Content = "Approve" } };
        var host = new Window { Content = peek, Width = 500, Height = 400 };
        host.Show();
        Settle(host);
        var parts = peek.GetVisualDescendants().OfType<Control>().ToList();
        var hairline = Brush("Brush.Hairline");
        var rules = parts.OfType<Border>().Where(b => b.BorderBrush is ISolidColorBrush s && s.Color == hairline).Select(b => b.BorderThickness).ToList();

        var head = parts.OfType<Border>().First(b => b.Name == "PART_Head");
        var close = parts.OfType<Button>().First(b => b.Name == "PART_Close");
        Assert.Equal(new Thickness(16, 0), head.Padding);   // the head is a fixed 48 with centred items: grammar min-height 48 plus 12 px padding and a hairline would be 49 (#3538)
        Assert.Equal(48, head.Bounds.Height, 0.5);
        Assert.Equal(23.5, (close.TranslatePoint(default, head)?.Y ?? double.NaN) + (close.Bounds.Height / 2), 0.5);   // centred in the 47 px above the hairline
        Assert.Equal(new Thickness(16, 8), parts.OfType<Border>().First(b => b.Name == "PART_FieldsBox").Padding);
        Assert.Equal(new Thickness(16, 12), parts.First(c => c.Name == "PART_Body").Margin);
        Assert.Equal(new Thickness(16, 12), parts.OfType<Border>().First(b => b.Name == "PART_PeekActionsBox").Padding);
        Assert.Equal(FontWeight.SemiBold, parts.OfType<TextBlock>().First(t => t.Name == "PART_Title").FontWeight);
        var label = parts.OfType<TextBlock>().First(t => t.Classes.Contains("peek-label"));
        Assert.Equal(96, ((Grid)label.Parent!).ColumnDefinitions[0].Width.Value, 0.01);
        Assert.Equal(2, rules.Count(t => t == new Thickness(0, 0, 0, 1)));   // under the head and under the fields
        _ = Assert.Single(rules, t => t == new Thickness(0, 1, 0, 0));   // above the actions
        host.Close();
        return true;
    });

    [Fact]
    public async Task Groups_sit_8_px_apart_and_the_list_pads_8_px_top_and_bottom() => await OnUi(() =>
    {
        var (window, list) = Show(RowGallery.Three(), height: 1400);   // tall enough to realize all three headers
        var headers = Headers(list);

        Assert.Equal(8, Top(headers[0], list), 0.5);   // the list's own padding, no group margin on the first group
        Assert.Equal(8 + 28 + (14 * 32) + 8, Top(headers[1], list), 0.5);
        Assert.Equal(8 + ((28 + (14 * 32) + 8) * 2), Top(headers[2], list), 0.5);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_sticky_copy_keeps_its_fill_when_a_header_loses_focus() => await OnUi(() =>
    {
        var (window, list) = Show(RowGallery.Three());
        list.ScrollTo(100);
        Settle(window);
        var sticky = list.GetVisualDescendants().OfType<RowGroupHeader>().Single(h => h.Name == "PART_Sticky");
        var fill = Brush("Brush.Canvas");

        _ = sticky.Focus(NavigationMethod.Pointer);   // a click on the copy would do the same
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
        Assert.True(row.Focus(NavigationMethod.Directional));
        Settle(window);

        Assert.False(sticky.Focusable);
        Assert.Equal(fill, Assert.IsType<ISolidColorBrush>(sticky.Background, exactMatch: false).Color);
        window.Close();
        return true;
    });
}
