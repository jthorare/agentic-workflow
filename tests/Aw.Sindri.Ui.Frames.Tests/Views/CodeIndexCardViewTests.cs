using System.Net;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Health's Code index card in the window (CI-10b): accessible names, and the text-fit canary (a long line wraps inside a narrow window, never clipped or pushed out).</summary>
[Lane(TestLane.Integration)]
public sealed class CodeIndexCardViewTests : FrameHost
{
    private const string LongRepo = "a-repository-with-an-unreasonably-long-name-that-would-not-fit-on-one-line-of-a-narrow-window";

    private static string Body(string repo) => $$"""{"repos":[{"repo":"{{repo}}","state":"ok","commit":"abcdef0123456789abcdef0123456789abcdef01","builtAt":"2026-10-20T12:00:00Z","layers":[{"layer":"structure","state":"ok","code":null,"detail":"ok"},{"layer":"graph","state":"unavailable","code":"SND-CODEIDX-020","detail":"graphify failed"},{"layer":"embeddings","state":"pending","code":null,"detail":"31 of 100 embedded"}],"counts":null}]}""";

    [Fact]
    public Task The_card_names_each_line_and_the_heading_for_assistive_technology() => OnUi(async () =>
    {
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Body("repo"));
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 0);
        shell.GoTo(PageId.System);
        Settle(window);

        var line = Assert.Single(Named<ItemsControl>(window, "CodeIndexLines").GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible);
        Assert.Equal(shell.Health.CodeIndex.Lines[0].Name, AutomationProperties.GetName(line));
        Assert.StartsWith("Code index: repo @ abcdef0", AutomationProperties.GetName(line), StringComparison.Ordinal);
        Assert.Equal(("Code index", "needs a look"), (AutomationProperties.GetName(Named<TextBlock>(window, "CodeIndexEyebrow")), AutomationProperties.GetName(Named<TextBlock>(window, "CodeIndexWord"))));
        Assert.False(Named<TextBlock>(window, "CodeIndexMessage").IsVisible);
    });

    [Fact]
    public Task The_card_follows_Views_in_the_overview() => OnUi(async () =>
    {
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Body("repo"));
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 0);
        shell.GoTo(PageId.System);
        Settle(window);

        // Both say whether the data is current (System spec), so the code index sits right after Views.
        var cards = Named<WrapPanel>(window, "SystemCards").Children;
        Assert.Equal(cards.IndexOf(Named<Border>(window, "ViewsCard")) + 1, cards.IndexOf(Named<Border>(window, "CodeIndexPanel")));
    });

    [Fact]
    public Task A_long_line_wraps_inside_a_narrow_window() => OnUi(async () =>
    {
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, Body(LongRepo));
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 0, width: 360, height: 800);
        shell.GoTo(PageId.System);
        Settle(window);

        var panel = Named<Border>(window, "CodeIndexPanel");
        var line = Named<ItemsControl>(window, "CodeIndexLines").GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible);
        var inWindow = line.TranslatePoint(new Point(line.Bounds.Width, 0), window)!.Value.X; // why: the line is in the tree
        Assert.Equal(TextWrapping.Wrap, line.TextWrapping);
        Assert.InRange(inWindow, 0, window.Width);
        Assert.True(line.Bounds.Height > line.FontSize * 2, "the long line takes more than one row, so it wrapped");
        Assert.InRange(panel.Bounds.Right, 0, window.Width);
        Frame(window, "health-code-index", "ci10b-");
    });
}
