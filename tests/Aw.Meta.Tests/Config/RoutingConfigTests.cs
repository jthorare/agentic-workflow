using Aw.Kernel.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;
using Aw.Meta.Tests.Adapters;
using static Aw.Meta.Tests.Routing.RoutingWorld;

namespace Aw.Meta.Tests.Config;

public sealed class RoutingConfigTests
{
    private static readonly TeamConfig _team = new("t1", "/work", ["meta", "api", "ui"], "meta");

    private static string? Problem(RoutingConfig routing) => ThresholdChecks.Problem(_team with { Routing = routing });

    [Fact, Trait("Lane", "unit")]
    public void Routing_defaults_to_no_models_no_pairs_a_switch_on_limit_and_a_ten_percent_floor()
    {
        var routing = _team.Routing;

        Assert.Equal((0, 0, 0, null, true, 10), (routing.Models.Count, routing.Equivalents.Count, routing.Lanes.Count, routing.DefaultModel, routing.SwitchWhenLimited, routing.MinRemainingPct));
        Assert.Null(Problem(routing));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_good_routing_passes() =>
        Assert.Null(Problem(Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["ui"] = new("codex", "gpt-5.5"), ["api"] = new(Model: "haiku") } }));

    [Theory, Trait("Lane", "unit")]
    [InlineData(-1)]
    [InlineData(101)]
    public void The_remaining_floor_is_zero_to_one_hundred(int pct) =>
        Assert.Contains("routing.minRemainingPct must be 0 to 100", Problem(Cfg(minRemainingPct: pct)), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_model_listed_under_two_providers_is_refused()
    {
        var routing = Cfg() with { Models = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["claude-code"] = ["opus"], ["codex"] = ["opus"] } };

        Assert.Contains("routing.models: 'opus' is listed under both claude-code and codex", Problem(routing), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_provider_key_must_be_an_agent_id_and_a_model_name_must_not_be_blank()
    {
        Assert.Contains("routing.models key must be an agent id", Problem(Cfg() with { Models = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["Codex!"] = ["a"] } }), StringComparison.Ordinal);
        Assert.Contains("routing.models.codex has a blank model name", Problem(Cfg() with { Models = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["codex"] = [" "] } }), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pair_must_name_two_listed_models_on_two_providers()
    {
        string?[] problems =
        [
            Problem(Cfg() with { Equivalents = new Dictionary<string, string>(StringComparer.Ordinal) { ["opus"] = "ghost" } }),
            Problem(Cfg() with { Equivalents = new Dictionary<string, string>(StringComparer.Ordinal) { ["opus"] = "sonnet" } }),
        ];

        Assert.Contains("routing.equivalents: 'ghost' is not listed under routing.models", problems[0], StringComparison.Ordinal);
        Assert.Contains("routing.equivalents: 'opus' and 'sonnet' are on the same provider", problems[1], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_entry_must_name_a_team_lane_a_listed_provider_and_a_model_that_provider_offers()
    {
        static string? With(string lane, LaneRoute route) => Problem(Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { [lane] = route } });

        Assert.Contains("routing.lanes.nope is not a lane of the team", With("nope", new("codex", "gpt-5.5")), StringComparison.Ordinal);
        Assert.Contains("routing.lanes.meta is the meta lane", With("meta", new("codex", "gpt-5.5")), StringComparison.Ordinal);
        Assert.Contains("routing.lanes.ui.agent 'cursor' has no entry in routing.models", With("ui", new("cursor", Model: null)), StringComparison.Ordinal);
        Assert.Contains("routing.lanes.ui: 'gpt-5.5' is not offered by claude-code", With("ui", new("claude-code", "gpt-5.5")), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Codex_meta_team_refuses_a_lane_routed_to_Claude_and_a_Claude_team_allows_it()
    {
        var routing = Cfg() with { Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["ui"] = new("claude-code", "sonnet") } };

        Assert.Contains("routing.lanes.ui: a codex meta can't message a Claude lane", ThresholdChecks.Problem(_team with { Agent = "codex", Routing = routing }), StringComparison.Ordinal);
        Assert.Null(ThresholdChecks.Problem(_team with { Routing = routing }));
        Assert.Null(ThresholdChecks.Problem(_team with { Agent = "claude-code", Routing = routing }));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_model_must_be_listed() =>
        Assert.Contains("routing.defaultModel 'ghost' is not listed under routing.models", Problem(Cfg(defaultModel: "ghost")), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_equivalent_of_a_model_is_its_pair_either_way_unless_several_map_to_it()
    {
        var routing = Cfg() with { Equivalents = new Dictionary<string, string>(StringComparer.Ordinal) { ["opus"] = "gpt-5.5", ["sonnet"] = "gpt-5.4-mini", ["haiku"] = "gpt-5.4-mini" } };

        Assert.Equal("gpt-5.5", routing.EquivalentOf("opus"));
        Assert.Equal("opus", routing.EquivalentOf("gpt-5.5"));
        Assert.Equal("gpt-5.4-mini", routing.EquivalentOf("haiku"));
        Assert.Null(routing.EquivalentOf("gpt-5.4-mini"));
        Assert.Equal("codex", routing.AgentOf("gpt-5.4-mini"));
        Assert.Null(routing.AgentOf("ghost"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_keys_are_read_from_the_team_file_by_name_and_saved_back()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "team.json");
        var config = new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default));
        await File.WriteAllTextAsync(path, """{"team":"t1","repo":"/w","lanes":["meta","ui"],"routing":{"models":{"claude-code":["opus"],"codex":["gpt-5.5"]},"equivalents":{"opus":"gpt-5.5"},"lanes":{"ui":{"agent":"codex","model":"gpt-5.5"}},"defaultModel":"opus","switchWhenLimited":false,"minRemainingPct":25}}""", TestContext.Current.CancellationToken);

        var loaded = (await config.Load(CancellationToken.None)).Value.Routing;

        Assert.Equal(["opus"], loaded.Models["claude-code"], StringComparer.Ordinal);
        Assert.Equal(("gpt-5.5", new LaneRoute("codex", "gpt-5.5"), "opus", false, 25), (loaded.Equivalents["opus"], loaded.Lanes["ui"], loaded.DefaultModel, loaded.SwitchWhenLimited, loaded.MinRemainingPct));

        _ = await config.Save((await config.Load(CancellationToken.None)).Value, CancellationToken.None);
        var again = (await config.Load(CancellationToken.None)).Value.Routing;

        Assert.Equal(new LaneRoute("codex", "gpt-5.5"), again.Lanes["ui"]);
        Assert.Equal("gpt-5.5", again.Equivalents["opus"]);

        await File.WriteAllTextAsync(path, """{"team":"t1","repo":"/w","lanes":["meta"],"routing":{"minRemainingPct":500}}""", TestContext.Current.CancellationToken);

        Assert.True((await config.Load(CancellationToken.None)).IsError);
    }
}
