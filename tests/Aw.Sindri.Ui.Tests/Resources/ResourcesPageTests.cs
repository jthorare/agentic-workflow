using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Resources;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Resources;

namespace Aw.Sindri.Ui.Tests.Resources;

/// <summary>The Resources page (PR 18b): the rows in words, Purge that asks first and names the owner, the refusal's reason, and the empty sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class ResourcesPageTests
{
    private static readonly Uri _url = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_page_lists_each_resource_with_owner_kind_name_and_size()
    {
        _server.SeedResources(
            new ResourceView("workflow:triage", "database", "triage.db", 1_258_291, "/state/triage.db"),
            new ResourceView("workflow:docs", "disk", "scratch", 2048, "/state/docs"));

        var page = await Shown();

        Assert.Equal(2, page.Rows.Count);
        Assert.Equal(("workflow:triage", "database", "triage.db", "1.2 MB"), (page.Rows[0].OwnerText, page.Rows[0].KindText, page.Rows[0].NameText, page.Rows[0].SizeText));
        Assert.Equal(("workflow:docs", "disk", "scratch", "2 KB"), (page.Rows[1].OwnerText, page.Rows[1].KindText, page.Rows[1].NameText, page.Rows[1].SizeText));
        Assert.False(page.IsEmpty);
    }

    [Fact]
    public async Task Purge_asks_first_naming_the_owner_then_sends_and_reloads()
    {
        _server.SeedResources(new ResourceView("workflow:triage", "database", "triage.db", 1024, "/state/triage.db"));
        var page = await Shown();
        var row = Assert.Single(page.Rows);

        row.AskPurge();

        Assert.True(row.IsConfirming);
        Assert.Contains("workflow:triage", row.ConfirmText, StringComparison.Ordinal);
        Assert.Equal(["list"], _server.ResourceCallLog);   // asking sends nothing
        await row.ConfirmPurge(Token);
        Assert.Equal(["list", "purge workflow:triage", "list"], _server.ResourceCallLog);
        Assert.Empty(page.Rows);
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public async Task Purge_is_refused_with_the_reason_while_the_owner_is_active()
    {
        _server.SeedResources(new ResourceView("workflow:triage", "database", "triage.db", 1024, "/state/triage.db"));
        _server.PurgeResourcesError = new Error("SND-RES-003", ErrorKind.Conflict, "workflow:triage has a run in progress");
        var page = await Shown();
        var row = Assert.Single(page.Rows);

        row.AskPurge();
        await row.ConfirmPurge(Token);

        Assert.Contains("workflow:triage has a run in progress", page.NoteText, StringComparison.Ordinal);
        _ = Assert.Single(page.Rows);
        Assert.False(row.IsConfirming);
    }

    [Fact]
    public async Task A_disk_shows_its_size_against_its_limit_and_a_database_shows_size_only()
    {
        _server.SeedResources(
            new ResourceView("workflow:docs", "disk", "scratch", 1_258_291, "/state/docs", LimitBytes: 5_242_880),
            new ResourceView("workflow:triage", "database", "triage.db", 1_258_291, "/state/triage.db"));

        var page = await Shown();

        Assert.Equal(2, page.Rows.Count);
        Assert.Equal(("1.2 MB of 5 MB", 0.24), (page.Rows[0].SizeLine, Math.Round(page.Rows[0].Fill ?? -1, 2, MidpointRounding.AwayFromZero)));
        Assert.Equal(("1.2 MB", null), (page.Rows[1].SizeLine, page.Rows[1].Fill));
    }

    [Fact]
    public async Task An_empty_list_shows_the_empty_text()
    {
        var page = await Shown();

        Assert.True(page.IsEmpty);
        Assert.Equal(Aw.Sindri.Ui.ViewModels.Labels.ResourcesEmpty, page.EmptyText);
        Assert.Empty(page.Rows);
    }

    private async Task<ResourcesPageViewModel> Shown()
    {
        var page = new ResourcesPageViewModel(_server);
        await page.Refresh(_url, Token);
        return page;
    }
}
