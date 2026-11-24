using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Frames.Panels.Tests.Views;

/// <summary>
/// The Health card's Context index line for a source the server flags with its own sentence (#3911): the sentence is the line, at 1000 and 1280. Each frame is
/// saved before the check, so the same file run on a tree without #3911 writes the before frames ("linear: pulling ...") and fails only on the line text.
/// "needs a look" draws the attention mark beside it, and the window is 900 high so the whole card, its bottom padding too, is in the frame at 1000 (#4129).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HealthLookWordsFramesTests : FrameHost
{
    private const string Words = "linear hasn't finished a sync in 15 minutes";   // why: a sentence as the server sends it; the app shows it whole

    private static string Status(string lookWords) =>
        $$"""{"sources":[{"source":"linear","phase":"live","usable":false,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":0,"lastCommit":null,"needsLook":true,"lookWords":{{lookWords}}}]}""";

    // The System page with the Context index card in view, once its line has drawn; the texts of its lines.
    private async Task<(Window Window, List<string> Lines)> Shown(string status, double width)
    {
        Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, status);
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.System);
        await Until(window, () => LineTexts(window).Exists(t => t.Contains("linear", StringComparison.Ordinal)), "the Context index card never drew the linear line");
        Named<Control>(window, "ContextIndexPanel").BringIntoView();
        Settle(window);
        return (window, LineTexts(window));
    }

    // The card's bottom edge in window coordinates; null when the card is not in the window.
    private static double? CardBottom(Window window)
    {
        var card = Named<Control>(window, "ContextIndexPanel");
        return card.TranslatePoint(new Point(0, card.Bounds.Height), window)?.Y;
    }

    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException(key + " is not a brush token");

    private static List<string> LineTexts(Window window) =>
        [.. window.GetVisualDescendants().OfType<ItemsControl>().Where(i => string.Equals(i.Name, "ContextIndexLines", StringComparison.Ordinal))
            .SelectMany(i => i.GetVisualDescendants().OfType<TextBlock>()).Select(t => t.Text ?? string.Empty)];

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task A_flagged_source_with_words_shows_the_servers_sentence_as_its_line(double width) => OnUi(async () =>
    {
        var (window, lines) = await Shown(Status("\"" + Words + "\""), width);
        FrameFile.Save(window, string.Create(CultureInfo.InvariantCulture, $"health-look-words-{width}"));

        Assert.Contains(lines, l => l.StartsWith(Words, StringComparison.Ordinal));
        Assert.True(CardBottom(window) <= window.ClientSize.Height, "the Context index card's bottom edge is outside the frame");
    });

    [Fact]
    public Task Needs_a_look_draws_the_attention_mark_beside_the_word() => OnUi(async () =>
    {
        var (window, _) = await Shown(Status("\"" + Words + "\""), 1280);

        var icon = window.GetVisualDescendants().OfType<StateIcon>().FirstOrDefault(i => string.Equals(i.Name, "ContextIndexGlyph", StringComparison.Ordinal));
        Assert.NotNull(icon);
        Assert.True(icon.IsEffectivelyVisible, "the Context index mark is hidden");
        Assert.Equal(StateGlyph.Warning, icon.Glyph);
        Assert.Equal(TokenColor("Brush.Attention"), Assert.IsAssignableFrom<ISolidColorBrush>(icon.Foreground).Color);
    });

    [Fact]
    public Task A_flagged_source_with_no_words_shows_its_state_line() => OnUi(async () =>
    {
        var (window, lines) = await Shown(Status("null"), 1280);
        FrameFile.Save(window, "health-look-no-words-1280");

        Assert.Contains(lines, l => l.StartsWith("linear: ", StringComparison.Ordinal));
    });
}
