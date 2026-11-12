using System.Globalization;
using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;
using Aw.Sindri.Ui.Views.Jobs;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A long list builds only the rows in view (#2776): the real page sits in the shell's page host (one ScrollViewer above every page) with 1000 rows served, and the window builds fewer than 100 row views.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class VirtualListsTests : FrameHost
{
    private const int Served = 1000;
    private const int Limit = 100;

    private static string Hex(int n) => n.ToString("x32", CultureInfo.InvariantCulture);

    [Fact]
    public Task Agents_list_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        var sessions = new StringBuilder();
        for (var i = 0; i < Served; i++)
        {
            var id = string.Create(CultureInfo.InvariantCulture, $"aaaaaaaa-0000-4000-8000-{i:x12}");
            _ = sessions.Append(i == 0 ? "" : ",").Append(CultureInfo.InvariantCulture, $$"""{"ref":"claude-code/{{id}}","provider":"claude-code","providerLabel":"Claude Code","id":"{{id}}","idKnown":true,"state":"running","asking":null,"stateSource":"record","estimated":false,"repo":"api","worktree":null,"host":{"kind":"unknown","label":"unknown","socketKind":null,"attachedClients":null,"terminal":null,"session":null,"pane":null,"socket":null},"lane":null,"launchId":null,"startedAt":null,"since":"2026-10-16T12:00:00.0000000Z","focus":{"method":"none","possible":false,"why":"not-built"},"title":null,"launch":null,"pid":{{i + 1}}}""");
        }

        Server.SessionAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"sessions":[{{sessions}}],"recentlyEnded":[],"problems":[],"counts":{"live":{{Served}},"asking":0,"outsideWorkflow":{{Served}}},"empty":null,"asOf":"2026-10-16T12:05:00.0000000Z"}""");
        var (window, shell) = await Connected(width: 1280, height: 800);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);

        var built = window.GetVisualDescendants().OfType<SessionRow>().Count();
        Assert.True(built is > 0 and < Limit, string.Create(CultureInfo.InvariantCulture, $"{built} session rows built for {Served} served"));
    });

    [Fact]
    public Task Jobs_list_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        var jobs = new StringBuilder();
        for (var i = 0; i < Served; i++)
        {
            _ = jobs.Append(i == 0 ? "" : ",").Append(CultureInfo.InvariantCulture, $$"""{"id":"JOB-{{Hex(i + 1)}}","kind":"EchoJob","key":"echo","item":null,"repo":null,"service":null,"state":"running","startedBy":"service:host","startedAt":"2026-01-01T00:00:00.0000000Z","endedAt":null,"rounds":0,"summary":null,"code":null,"models":null,"waitingOn":null,"waitingLine":null}""");
        }

        Server.JobsAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"jobs":[{{jobs}}],"empty":null,"asOf":{"cursor":4,"ledgerHead":4,"projectedAt":"2026-01-01T00:00:00.0000000Z","rebuilding":false,"stalled":false}}""");
        var (window, shell) = await Connected(width: 1280, height: 800);
        shell.GoTo(PageId.Jobs);
        await Until(window, () => shell.Jobs.Rows.Count == Served, "the Jobs page never drew the served rows");

        var built = window.GetVisualDescendants().OfType<JobRow>().Count();
        Assert.True(built is > 0 and < Limit, string.Create(CultureInfo.InvariantCulture, $"{built} job rows built for {Served} served"));
    });

    [Fact]
    public Task Memory_list_builds_only_the_rows_in_view() => OnUi(async () =>
    {
        for (var i = 0; i < Served; i++)
        {
            _ = Server.SeedMemory(string.Create(CultureInfo.InvariantCulture, $"store-{i:D4}"));
        }

        var (window, shell) = await Connected(width: 1280, height: 800);
        shell.GoTo(PageId.Memory);
        await Until(window, () => shell.MemoryPage.Stores.Count == Served, "the Memory page never drew the served stores");

        var built = window.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "MemoryStoreFrame");
        Assert.True(built is > 0 and < Limit, string.Create(CultureInfo.InvariantCulture, $"{built} store rows built for {Served} served"));
    });
}
