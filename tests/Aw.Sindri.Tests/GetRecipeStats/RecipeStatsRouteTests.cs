using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.GetRecipeStats;

/// <summary>
/// 'GET /workflow/recipes/{id}/stats' and the recipe list's stats and status word on the real host (API spec §3.8, E-24, E-25, P12), over a fixture ledger of
/// workflow.item runs: one done without a person, one done after a park was answered, one cancelled, one failed.
/// </summary>
public sealed class RecipeStatsRouteTests : IAsyncLifetime
{
    private const string Item = "/sindri/workflow/recipes/workflow.item/stats";
    private GoldenHost _host = null!;
    private (string Name, string Value)[] _human = [];
    private string _itemHash = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(_host, TestContext.Current.CancellationToken);
        _human = Human(_host.Token.Value);
        _ = await Register("sindri/workflow.step.yaml");
        _itemHash = await Register("sindri/workflow.item.yaml");
        var ledger = new StatLedger(_host, "workflow.item", _itemHash);
        await ledger.Start("T-1/item/1", 0);
        await ledger.Move("T-1/item/1", 0.5, "produce", "verify", tokens: 1_000);
        await ledger.End("T-1/item/1", 1, "verify", "done", "resolved");
        await ledger.Start("T-2/item/1", 2);
        await ledger.Park("T-2/item/1", 2.25, "produce", ParkReason.NoProgress);
        await ledger.Answer("T-2/item/1", 2.75, "produce");
        await ledger.End("T-2/item/1", 3, "produce", "done", "resolved");
        await ledger.Start("T-3/item/1", 4);
        await ledger.End("T-3/item/1", 4.5, "produce", "cancelled", "cancelled");
        await ledger.Start("T-4/item/1", 5);
        await ledger.End("T-4/item/1", 5.5, "produce", "failed", "failed");
        ledger.At(19 * 24);   // 2026-01-20: the fixture runs started 19 days ago
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact, Trait("Lane", "integration")]
    public async Task The_stats_match_the_fixture_ledger_in_the_golden_body()
    {
        var served = await _host.Http.Ask(Get(Item + "?window=30d", _human));

        Assert.True(served.Status == HttpStatusCode.OK, served.Body);
        ApiGolden.Match(served.Body, _host.Home, "workflow-recipes-id-stats", "workflow-item");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_default_window_is_30_days_and_7_days_leaves_out_the_older_runs()
    {
        var thirty = JsonDocument.Parse((await _host.Http.Ask(Get(Item, _human))).Body).RootElement;
        var seven = JsonDocument.Parse((await _host.Http.Ask(Get(Item + "?window=7d", _human))).Body).RootElement;

        Assert.Equal(("30d", 4), (thirty.GetProperty("window").GetString(), thirty.GetProperty("runs").GetInt32()));
        Assert.Equal(("7d", 0), (seven.GetProperty("window").GetString(), seven.GetProperty("runs").GetInt32()));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_hash_scopes_the_stats_to_the_runs_under_it()
    {
        var under = JsonDocument.Parse((await _host.Http.Ask(Get($"{Item}?hash={_itemHash}", _human))).Body).RootElement;
        var other = JsonDocument.Parse((await _host.Http.Ask(Get($"{Item}?hash=sha256:{new string('a', 64)}", _human))).Body).RootElement;

        Assert.Equal((4, _itemHash), (under.GetProperty("runs").GetInt32(), under.GetProperty("hash").GetString()));
        Assert.Equal(0, other.GetProperty("runs").GetInt32());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("?window=1y")]
    [InlineData("?window=")]
    [InlineData("?hash=not-a-hash")]
    [InlineData("?hash=sha256:abc")]
    public async Task A_window_or_hash_outside_its_set_is_SND_API_013(string query)
    {
        var served = await _host.Http.Ask(Get(Item + query, _human));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-API-013"), (served.Status, served.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unregistered_recipe_is_SND_RECIPE_002()
    {
        var served = await _host.Http.Ask(Get("/sindri/workflow/recipes/workflow.unknown/stats", _human));

        Assert.Equal((HttpStatusCode.NotFound, "SND-RECIPE-002"), (served.Status, served.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_recipe_that_is_only_ever_a_child_returns_child_only_with_null_run_figures()
    {
        var served = JsonDocument.Parse((await _host.Http.Ask(Get("/sindri/workflow/recipes/workflow.step/stats", _human))).Body).RootElement;

        Assert.True(served.GetProperty("childOnly").GetBoolean());
        Assert.Equal(JsonValueKind.Null, served.GetProperty("runs").ValueKind);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_list_gives_each_recipe_its_status_word_and_the_default_window_stats_apart_from_a_draft_and_a_child_only_recipe()
    {
        var served = await _host.Http.Ask(Get("/sindri/workflow/recipes", _human));
        var listed = JsonDocument.Parse(served.Body).RootElement;

        Assert.Equal("30d", listed.GetProperty("window").GetString());
        var recipes = listed.GetProperty("recipes").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetString()!, r => r, StringComparer.Ordinal);
        Assert.Equal(("draft", false), (recipes["workflow.item"].GetProperty("status").GetString(), recipes["workflow.item"].GetProperty("childOnly").GetBoolean()));
        Assert.Equal(("draft", true), (recipes["workflow.step"].GetProperty("status").GetString(), recipes["workflow.step"].GetProperty("childOnly").GetBoolean()));
        Assert.False(recipes["workflow.item"].TryGetProperty("stats", out _));   // a draft has none
        Assert.False(recipes["workflow.step"].TryGetProperty("stats", out _));   // a child-only recipe has none
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_list_window_outside_its_set_is_SND_API_013()
    {
        var served = await _host.Http.Ask(Get("/sindri/workflow/recipes?window=1y", _human));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-API-013"), (served.Status, served.Code));
    }

    private async Task<string> Register(string path)
    {
        var body = JsonSerializer.Serialize(new { text = ShippedRecipes.Read(path), file = Path.GetFileName(path) });
        var registered = await _host.Http.Ask(Post("/sindri/workflow/recipes", body, _human));
        Assert.True(registered.Status == HttpStatusCode.OK, registered.Body);
        return JsonDocument.Parse(registered.Body).RootElement.GetProperty("hash").GetString()!;   // why: a registered response always carries the hash
    }
}
