using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// The review page's contrast row (review page spec §6.4 "Tests", Ph7): every text on an added or removed diff line, line numbers
/// included, reads at 4.5:1 or better on its tint, from the brushes the page really draws rather than from the token file.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewDiffContrastTests : FrameHost
{
    private const double TextAa = 4.5;

    private static string Hex(IBrush? brush) =>
        brush is ISolidColorBrush solid
            ? $"#{solid.Color.R:x2}{solid.Color.G:x2}{solid.Color.B:x2}"
            : throw new InvalidOperationException("not a solid brush: " + brush);

    [Theory]
    [InlineData("added")]
    [InlineData("removed")]
    public Task Every_text_on_a_tinted_diff_line_reads_at_4_5_to_1(string kind) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewDiffLine"), "the review page never showed its diff");

        var line = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ReviewDiffLine" && b.Classes.Contains(kind) && b.IsEffectivelyVisible);
        var background = Hex(line.Background);
        var texts = line.GetVisualDescendants().OfType<TextBlock>().ToList();
        Frame(window, "review-diff-" + kind, "ph7-");

        Assert.Equal(4, texts.Count);   // old number, new number, sign, text
        Assert.All(texts, t =>
        {
            var ratio = Wcag.Contrast(Hex(t.Foreground), background);
            Assert.True(ratio >= TextAa, string.Create(CultureInfo.InvariantCulture, $"\"{t.Text}\" ({string.Join(' ', t.Classes)}) on the {kind} tint is {ratio:F2}"));
        });
    });
}
