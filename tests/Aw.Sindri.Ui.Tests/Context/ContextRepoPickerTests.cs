using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The repo picker: it shows only when the whole map says it covers two repos or more; picking one reads that repo (repo=) and gives the Files view its own top.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextRepoPickerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    // The overview golden with a repos summary added; no names at all leaves the field out, as an older Sindri would.
    private static FakeSindriApi Serving(params string[] repos) => new()
    {
        WholeMapAnswer = q =>
        {
            var json = GoldenFiles.Api("context-whole", "nodes-small");
            if (repos.Length > 0)
            {
                var node = JsonNode.Parse(json)!;
                node["repos"] = new JsonArray([.. repos.Select(r => (JsonNode)new JsonObject { ["name"] = r, ["nodeCount"] = 3 })]);
                json = node.ToJsonString();
            }

            return new RouteAnswer(HttpStatusCode.OK, json);
        },
    };

    [Fact]
    public async Task A_map_that_does_not_list_its_repos_is_one_repo_with_no_picker()
    {
        var page = Page(Serving());

        await page.Opened(Token);

        Assert.Equal((false, 0), (page.HasRepoPicker, page.Repos.Count));
    }

    [Fact]
    public async Task One_repo_in_the_list_shows_no_picker()
    {
        var page = Page(Serving("alpha"));

        await page.Opened(Token);

        Assert.False(page.HasRepoPicker);
    }

    [Fact]
    public async Task Two_repos_show_the_picker_with_both_names()
    {
        var page = Page(Serving("beta", "alpha"));

        await page.Opened(Token);

        Assert.True(page.HasRepoPicker);
        Assert.Equal(["alpha", "beta"], page.Repos);
        Assert.Equal("alpha", page.SelectedRepo);
    }

    [Fact]
    public async Task Picking_a_repo_reads_the_whole_map_with_that_repo()
    {
        var api = Serving("alpha", "beta");
        var page = Page(api);
        await page.Opened(Token);

        await page.PickRepo("beta", Token);

        Assert.Equal([null, "beta"], api.WholeMapAsked.Select(q => q.Repo), StringComparer.Ordinal);
        Assert.Equal("beta", page.SelectedRepo);
    }

    [Fact]
    public async Task Picking_the_repo_already_read_reads_nothing()
    {
        var api = Serving("alpha", "beta");
        var page = Page(api);
        await page.Opened(Token);

        await page.PickRepo("alpha", Token);

        _ = Assert.Single(api.WholeMapAsked);
    }

    [Fact]
    public async Task Picking_a_repo_gives_the_files_view_that_repos_top()
    {
        var api = Serving("alpha", "beta");
        var page = Page(api);
        await page.Opened(Token);
        page.ChooseView(files: true);

        await page.PickRepo("beta", Token);

        var last = api.FileMapAsked[^1];
        Assert.Equal("beta", last.Repo);
        Assert.Null(last.Path);
    }
}
