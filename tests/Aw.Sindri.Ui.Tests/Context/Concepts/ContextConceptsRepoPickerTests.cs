using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>#3696: a page that opens on the concept cards never reads the whole map, so the concepts answer's repo list is what shows the repo picker.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptsRepoPickerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Three concepts with the repos field set to these names; no names leaves the field out, as a Sindri from before #3696 would.
    private static RouteAnswer ConceptsOver(params string[] repos)
    {
        var node = JsonNode.Parse(Encoding.UTF8.GetString(AwJson.Serialize(AConcepts.Three(), SindriApiClient.Json)))!.AsObject();
        if (repos.Length > 0)
        {
            node["repos"] = new JsonArray([.. repos.Select(r => (JsonNode)new JsonObject { ["name"] = r, ["nodeCount"] = 3 })]);
        }

        return new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
    }

    [Fact]
    public async Task Concepts_over_two_repos_show_the_picker_without_a_whole_map_read()
    {
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => ConceptsOver("beta", "alpha") };
        var page = await ConceptPage.Made(api);

        await page.Opened(Token);

        Expect.Equal((true, true, 0), (page.Concepts.Find("concept:billing") is not null, page.HasRepoPicker, api.WholeMapAsked.Count));
        Assert.Equal(["alpha", "beta"], page.Repos);
        Assert.Equal("alpha", page.SelectedRepo);
    }

    [Fact]
    public async Task Concepts_that_do_not_list_repos_show_no_picker()
    {
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => ConceptsOver() };
        var page = await ConceptPage.Made(api);

        await page.Opened(Token);

        Assert.Equal((false, 0), (page.HasRepoPicker, page.Repos.Count));
    }

    [Fact]
    public async Task After_a_pick_the_picker_stays_with_both_repos()
    {
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => ConceptsOver("alpha", "beta") };
        var page = await ConceptPage.Made(api);
        await page.Opened(Token);

        await page.PickRepo("beta", Token);

        Expect.Equal((true, "beta"), (page.HasRepoPicker, page.SelectedRepo));
        Assert.Equal(["alpha", "beta"], page.Repos);
        Assert.Equal("beta", api.ContextConceptsAsked[^1].Repo);
    }
}
