using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #3503: at rest a Button.hold-part is marked by its label, not by its edge. Its border is the HairlineStrong hairline (hover is not a required state, and the part's edge is not what identifies it, WCAG 1.4.11),
/// so the label must be there, visible, and read on the part's own fill at 4.5:1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HoldPartRestMarkerTests
{
    private const string Label = "Hold to confirm";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HoldPartRestMarkerTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static Color Solid(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : throw new InvalidOperationException("a solid brush was expected");

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    [Fact]
    public async Task A_hold_part_at_rest_carries_its_label_and_the_label_reads_on_the_parts_fill()
    {
        var (shown, ink, fill) = await OnUi(() =>
        {
            var button = new Button { Content = Label };
            button.Classes.Add("hold-part");
            var window = new Window { Width = 400, Height = 120, Content = button };
            window.Show();
            window.UpdateLayout();
            var text = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => string.Equals(t.Text, Label, StringComparison.Ordinal) && t.IsEffectivelyVisible);
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            var result = (text is not null, Solid(text?.Foreground), Solid(presenter.Background));
            window.Close();
            return result;
        });

        Assert.True(shown, "a hold part at rest shows its label");
        Assert.True(Contrast(ink, fill) >= 4.5, "the label reads on the part's fill at 4.5:1");
    }
}
