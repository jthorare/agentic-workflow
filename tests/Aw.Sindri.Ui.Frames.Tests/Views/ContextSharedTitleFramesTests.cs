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

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// A shared name (SND-CONTEXT-037): the "Did you mean" choices that share a title each draw their key's path, muted, after it; a unique title draws none (#3124).
/// Each frame is saved before the checks, so the same file run on a tree without #3124 writes the before frames (two rows that both read "layout:panes").
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextSharedTitleFramesTests : FrameHost
{
    private const string Split = "feature:tabs.pane-split";
    private const string Pane = "thing:pane-split";
    private const string Layout = "concept:layout.grid";

    private static readonly RouteAnswer _shared = new(HttpStatusCode.Conflict,
        $$"""{"type":"about:blank","title":"SND-CONTEXT-037","status":409,"detail":"'layout:panes' names more than one node","traceId":"<trace>","candidates":["{{Split}}","{{Pane}}","{{Layout}}"],"choices":[{"key":"{{Split}}","label":"layout:panes","kind":"feature"},{"key":"{{Pane}}","label":"layout:panes","kind":"thing"},{"key":"{{Layout}}","label":"Grid layout","kind":"concept"}]}""");

    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException(key + " is not a brush token");

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task Choices_that_share_a_title_draw_their_paths_muted(double width) => OnUi(async () =>
    {
        Server.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.InternalServerError, "{}");
        Server.LensesAnswer = _ => _shared;
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Context);
        shell.Context.AnchorText = "layout:panes";
        await shell.Context.Submit(Token);
        Settle(window);
        FrameFile.Save(window, string.Create(CultureInfo.InvariantCulture, $"context-shared-title-{width}"));

        var rows = Named<Control>(window, "ContextChoices").GetVisualDescendants().OfType<Button>().ToList();
        Assert.Equal(3, rows.Count);
        var paths = rows.Select(r => r.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => string.Equals(t.Name, "ChoicePath", StringComparison.Ordinal))).ToList();
        Assert.Equal([" · tabs.pane-split", " · pane-split"], paths.Take(2).Select(p => p is { IsEffectivelyVisible: true } ? p.Text ?? string.Empty : string.Empty), StringComparer.Ordinal);
        Assert.All(paths.Take(2), p => Assert.Equal(TokenColor("Brush.InkSubtle"), Assert.IsAssignableFrom<ISolidColorBrush>(p?.Foreground).Color));
        Assert.False(paths[2] is { IsEffectivelyVisible: true }, "a unique title drew a path");
        Assert.All(rows, r => Assert.InRange(r.TranslatePoint(new Point(r.Bounds.Width, 0), window)?.X ?? double.MaxValue, 0d, window.Bounds.Width));
    });
}
