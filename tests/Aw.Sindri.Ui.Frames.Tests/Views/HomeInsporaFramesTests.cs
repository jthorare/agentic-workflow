using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Home's visual pass (#2780): one display number, one accent (one primary button), borderless 24 px cards with sentence-case titles, and 6 px chips.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeInsporaFramesTests : FrameHost
{
    [Theory]
    [InlineData(1280, true)]
    [InlineData(1280, false)]
    [InlineData(1000, true)]
    [InlineData(1000, false)]
    public Task Home_has_one_display_number_borderless_24_px_cards_and_sentence_case_titles(double width, bool typical) => OnUi(async () =>
    {
        if (!typical)
        {
            Server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-nothing");
            Server.QueueView = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "empty"));
        }

        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Home);
        Settle(window);
        var home = window.GetVisualDescendants().OfType<HomePage>().Single(h => h.IsEffectivelyVisible);

        OneDisplayNumber(home, shell.Attention.NeedsYouNumber);
        BorderlessCardsWith24Insets(home);
        SentenceCaseTitles(home);
        OnePrimaryButton(window, home);
        SixPixelChips(home);
        ElsewhereLineStartsUnderTheTodoTitle(home);

        Frame(window, string.Create(CultureInfo.InvariantCulture, $"home-inspora-{width:0}-{(typical ? "typical" : "empty")}"), prefix: "");
    });

    // The need-you count is the only display text; at zero there is none (the empty sentence says so).
    private static void OneDisplayNumber(HomePage home, string number)
    {
        var displays = home.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("display")).ToList();
        if (number.Length == 0)
        {
            Assert.Empty(displays);
            return;
        }

        var only = Assert.Single(displays);
        Assert.Equal(("NeedsYouNumber", number), (only.Name, only.Text));
    }

    // A card is a Surface1 step with no border, and its header and plain rows start 24 px in.
    private static void BorderlessCardsWith24Insets(HomePage home)
    {
        var panels = home.GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("panel")).ToList();
        Assert.NotEmpty(panels);
        Assert.All(panels, panel =>
        {
            Assert.Equal(new Thickness(0), panel.BorderThickness);
            var head = panel.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("phead"));
            Assert.Equal(new Thickness(24, 0), head.Padding);
        });

        var plainRows = home.GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("item-row")
            && !b.Classes.Contains("needs-you-row") && !b.Classes.Contains("attention-row") && !b.Classes.Contains("selected")).ToList();
        Assert.All(plainRows, row => Assert.Equal(new Thickness(24, 0), row.Padding));
    }

    // No caps captions: a card's title has lower-case letters in it.
    private static void SentenceCaseTitles(HomePage home)
    {
        var texts = home.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
        Assert.DoesNotContain(texts, t => t.Classes.Contains("eyebrow"));
        var titles = texts.Where(t => t.Classes.Contains("section-title")).ToList();
        Assert.True(titles.Count >= 3, "Needs you, Running and Queued each have a title");
        Assert.All(titles, t => Assert.NotEqual(t.Text, t.Text?.ToUpperInvariant(), StringComparer.Ordinal));
    }

    // Primary is the one button the page wants pressed: as drawn, at most one button on Home wears the primary fill, and the setup button is not it.
    private static void OnePrimaryButton(Window window, HomePage home)
    {
        var primary = ((ISolidColorBrush)Resource(window, "Brush.Primary")).Color;
        var filled = home.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && Fill(b) == primary).ToList();
        Assert.True(filled.Count <= 1, string.Join(", ", filled.Select(b => b.Name ?? b.Content?.ToString())) + " all wear the primary fill");

        foreach (var setup in home.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "SetupHomeContinue" && b.IsEffectivelyVisible))
        {
            Assert.DoesNotContain("primary", setup.Classes, StringComparer.Ordinal);
        }
    }

    // Chips are 6 px (Radius.SM): the state chips on rows and the badges.
    private static void SixPixelChips(HomePage home)
    {
        var chips = home.GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && (b is StateChipView || b.Classes.Contains("badge") || b.Classes.Contains("phrase-chip"))).ToList();
        Assert.All(chips, chip => Assert.Equal(new CornerRadius(6), chip.CornerRadius));
    }

    // The Todo card's "From elsewhere" footer starts where its title does.
    private static void ElsewhereLineStartsUnderTheTodoTitle(HomePage home)
    {
        var title = home.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "TodoTitle" && t.IsEffectivelyVisible);
        var words = home.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "ElsewhereFrame" && b.IsEffectivelyVisible)?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.IsEffectivelyVisible);
        if (title is null || words is null)
        {
            return;
        }

        var titleLeft = title.TranslatePoint(default, home)!.Value.X;   // why: both are in Home's visual tree
        var wordsLeft = words.TranslatePoint(default, home)!.Value.X;
        Assert.InRange(Math.Abs(titleLeft - wordsLeft), 0, 1);
    }

    private static Color? Fill(Button button) =>
        button.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(p => p.Name == "PART_ContentPresenter")?.Background is ISolidColorBrush brush ? brush.Color : null;

    private static object Resource(Window window, string key) =>
        window.TryFindResource(key, window.ActualThemeVariant, out var value) && value is not null ? value : throw new InvalidOperationException($"{key} is not a resource");
}
