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

    private static string TwoRepos(string a, string b) => """{"repos":[""" + Repo(a) + "," + Repo(b) + "]}";

    private static string Repo(string name) => $$"""{"repo":"{{name}}","state":"ok","commit":"abcdef0123456789abcdef0123456789abcdef01","builtAt":"2026-10-20T12:00:00Z","layers":[{"layer":"structure","state":"ok","code":null,"detail":"ok"}],"counts":null}""";

    // #3577: two repo lines that both wrap touched, because the row Border had a MinHeight and the list no spacing.
    [Fact]
    public Task Two_wrapped_repo_lines_keep_a_gap_between_them() => OnUi(async () =>
    {
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, TwoRepos(LongRepo, LongRepo + "-2"));
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 1, width: 360, height: 800);
        shell.GoTo(PageId.System);
        Settle(window);

        var rows = Named<ItemsControl>(window, "CodeIndexLines").ItemsPanelRoot!.Children;
        var space = Assert.IsType<double>(Application.Current!.FindResource("Space.XS"));
        Assert.Equal(2, rows.Count);
        Assert.True(rows[1].Bounds.Top - rows[0].Bounds.Bottom >= space, "the second line starts a Space.XS below the first");
    });

    // #3576: the resume command is its own selectable, unwrapped row in the mono style.
    [Fact]
    public Task A_paused_repo_shows_the_resume_command_on_its_own_row() => OnUi(async () =>
    {
        Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, """{"repos":[{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[{"repo":"repo","state":"missing","embeddingsPercent":null,"waiting":null,"paused":true}]}""");
        var (window, shell) = await Connected(s => s.Health.CodeIndex.Lines.Count > 0);
        shell.GoTo(PageId.System);
        Settle(window);

        var command = Named<SelectableTextBlock>(window, "CodeIndexCommand");
        Assert.Equal(("sindri services resume code-index-main-move", TextWrapping.NoWrap, true), (command.Text, command.TextWrapping, command.IsEffectivelyVisible));
        Assert.True(command.Classes.Contains("mono"), "the command takes the mono style");
        var line = Assert.Single(Named<ItemsControl>(window, "CodeIndexLines").GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Name != "CodeIndexCommand");
        Assert.DoesNotContain("sindri services", line.Text, StringComparison.Ordinal);
    });
}
