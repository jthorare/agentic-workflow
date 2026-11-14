using System.Net;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

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
        var cards = Named<Panel>(window, "SystemCards").Children;
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

    // #3577, qa round 4: the ink gap (text bottom to next text top) was 31, 23, 15 or 7 px depending on whether a line wrapped, and last line to note was another 23.
    // Every gap on the card is now one thing: a line's own padding on each side, so wrapped or not they are equal and the last line meets the note the same way.
    [Fact]
    public Task Every_gap_between_repo_lines_and_before_the_note_is_the_same() => OnUi(async () =>
    {
        string[] names = [LongRepo, LongRepo + "-2", "short-a", "short-b"];
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"repos\":[" + string.Join(',', names.Select(Missing)) + "],\"health\":[" + string.Join(',', names.Select(Paused)) + "]}");
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 3, width: 360, height: 1000);
        shell.GoTo(PageId.System);
        Settle(window);

        var lines = Named<ItemsControl>(window, "CodeIndexLines").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
        var note = Named<TextBlock>(window, "CodeIndexPausedNote");
        Assert.Equal(4, lines.Count);
        Assert.True(lines[0].Bounds.Height > lines[2].Bounds.Height * 1.5 && lines[1].Bounds.Height > lines[3].Bounds.Height * 1.5, "the long lines wrap and the short ones do not");
        double Ink(TextBlock above, TextBlock below) => below.TranslatePoint(new Point(0, 0), window)!.Value.Y - above.TranslatePoint(new Point(0, above.Bounds.Height), window)!.Value.Y;
        var gaps = new[] { Ink(lines[0], lines[1]), Ink(lines[1], lines[2]), Ink(lines[2], lines[3]), Ink(lines[3], note) };
        var space = Assert.IsType<double>(Application.Current!.FindResource("Space.XS"));
        Assert.All(gaps, g => Assert.Equal(gaps[0], g, 1));
        Assert.True(gaps[0] >= space, "a gap is at least Space.XS");
    });

    // #3576: one command for the whole card (the pause is one service), under its own words, mono and unwrapped.
    [Fact]
    public Task Three_paused_repos_show_the_resume_command_once_directly_under_its_text() => OnUi(async () =>
    {
        string[] names = ["repo-a", "repo-b", "repo-c"];
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"repos\":[" + string.Join(',', names.Select(Missing)) + "],\"health\":[" + string.Join(',', names.Select(Paused)) + "]}");
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 2);
        shell.GoTo(PageId.System);
        Settle(window);

        var panel = Named<Border>(window, "CodeIndexPanel");
        var command = Assert.Single(panel.GetVisualDescendants().OfType<SelectableTextBlock>(), c => c.Name == "CodeIndexCommand");
        var note = Named<TextBlock>(window, "CodeIndexPausedNote");
        var space = Assert.IsType<double>(Application.Current!.FindResource("Space.XS"));
        Assert.Equal(("sindri services resume code-index-main-move", TextWrapping.NoWrap, true), (command.Text, command.TextWrapping, command.IsEffectivelyVisible));
        Assert.True(command.Classes.Contains("mono"), "the command takes the mono style");
        Assert.Equal("Index builds are paused for every repo:", note.Text);
        Assert.InRange(command.TranslatePoint(new Point(0, 0), window)!.Value.Y - note.TranslatePoint(new Point(0, note.Bounds.Height), window)!.Value.Y, 0, space);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name != "CodeIndexCommand"), t => t.Text?.Contains("resume", StringComparison.Ordinal) == true);
    });

    private static string Missing(string name) => $$"""{"repo":"{{name}}","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}""";

    private static string Paused(string name) => $$"""{"repo":"{{name}}","state":"missing","embeddingsPercent":null,"waiting":null,"paused":true}""";
}
