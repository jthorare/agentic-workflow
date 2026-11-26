using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The collector's stale-list rows (#4244) are about twice as long as the plain overrun row. They fit at 1000 px (the window's minimum) and at 1440 px: on the
/// System page's Agents card and on the Agents page, each text takes at most two lines and nothing is cut. A fake row holds each text. Each frame is saved before
/// the check, under frames/ as agents-stale-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AgentsStaleRowFramesTests : FrameHost
{
    private const string Aged = "The agent list is from 2 min ago: the last 3 passes took longer than their 500 ms budget";
    private const string NoList = "No agent list yet: the last 3 passes took longer than their 500 ms budget";

    public static TheoryData<string, string, int> Cases => new()
    {
        { "aged", Aged, 1000 },
        { "aged", Aged, 1440 },
        { "no-list", NoList, 1000 },
        { "no-list", NoList, 1440 },
    };

    private static void AssertFits(ItemsControl problems, string text)
    {
        var line = problems.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Inlines?.Count > 0 ? t.Inlines.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == text) : t.Text == text);
        var lines = line.TextLayout.TextLines;
        Assert.InRange(lines.Count, 1, 2);
        Assert.DoesNotContain(lines, static l => l.HasCollapsed);
        Assert.True(line.Bounds.Width <= problems.Bounds.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"the row is {line.Bounds.Width} px wide in a {problems.Bounds.Width} px column"));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_stale_row_fits_the_System_pages_Agents_card(string name, string text, int width) => OnUi(async () =>
    {
        var rows = new[] { new SessionProblemView("collector", "SND-SESSION-001", text) };
        Server.Current = A.Status() with { Sessions = new SessionCountsView(Live: 2, Asking: 0, Problems: 1, Running: 2, ProblemRows: rows) };
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.System);
        await Until(window, () => Visible(window, "AgentsProblems"), "the Agents card never drew its problem row");
        Named<Control>(window, "AgentsCard").BringIntoView();

        Frame(window, string.Create(CultureInfo.InvariantCulture, $"card-{name}-{width}"), "agents-stale-");
        AssertFits(window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "AgentsProblems" && c.IsEffectivelyVisible), text);
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_stale_row_fits_the_Agents_page(string name, string text, int width) => OnUi(async () =>
    {
        var body = GoldenFiles.Api("sessions", "problems-source-failed")
            .Replace("\"claude-records\"", "\"collector\"", StringComparison.Ordinal)
            .Replace("The claude-records source couldn't be read in the last pass", text, StringComparison.Ordinal);
        Server.SessionAnswer = new RouteAnswer(HttpStatusCode.OK, body);
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.ProblemLines.Count > 0 && shell.Agents.Groups.Count > 0);

        Frame(window, string.Create(CultureInfo.InvariantCulture, $"page-{name}-{width}"), "agents-stale-");
        Assert.Equal([text], shell.Agents.ProblemLines);
        AssertFits(Named<ItemsControl>(window, "AgentsProblems"), text);
    });
}
