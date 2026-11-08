using Aw.Meta.Core.Config;
using Aw.Meta.Core.Routing;
using Aw.Meta.Features.Routing;
using static Aw.Meta.Tests.Routing.RoutingWorld;

namespace Aw.Meta.Tests.Routing;

public sealed class CodexModelCheckTests
{
    private static readonly HashSet<string> _known = new(["gpt-5.5", "gpt-5.4", "gpt-5.4-mini"], StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Names_codex_lists_give_no_warning() =>
        Assert.Empty(CodexModelCheck.Warnings(Cfg(), _known));

    [Fact, Trait("Lane", "unit")]
    public void A_listed_codex_model_and_a_lane_entry_that_Codex_does_not_know_are_each_named_once()
    {
        var routing = Cfg() with
        {
            Models = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["claude-code"] = ["opus"], ["codex"] = ["gpt-5.5", "gpt-5.55"] },
            Lanes = new Dictionary<string, LaneRoute>(StringComparer.Ordinal) { ["ui"] = new("codex", "gpt-5.55"), ["api"] = new("codex", "gpt-5.6") },
            Equivalents = new Dictionary<string, string>(StringComparer.Ordinal),
            DefaultModel = null,
        };

        var warnings = CodexModelCheck.Warnings(routing, _known);

        Assert.Equal(2, warnings.Count);
        Assert.Contains("routing.models.codex names codex model 'gpt-5.55', which ~/.codex/models_cache.json does not list", warnings[0], StringComparison.Ordinal);
        Assert.Contains("routing.lanes.api names codex model 'gpt-5.6'", warnings[1], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Route_carries_the_warning_when_the_cache_exists_and_none_when_it_does_not()
    {
        var w = new RoutingWorld();
        await w.Ready(Cfg() with { Models = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["claude-code"] = ["opus"], ["codex"] = ["gpt-5.5", "gpt-5.55"] }, Equivalents = new Dictionary<string, string>(StringComparer.Ordinal) { ["opus"] = "gpt-5.5" } });

        var without = await w.Service().Handle(new RouteRequest(), CancellationToken.None);
        w.U.World.CodexModels.List("gpt-5.5");
        var with = await w.Service().Handle(new RouteRequest(), CancellationToken.None);

        Assert.Empty(without.Value.Warnings!);
        Assert.Contains("gpt-5.55", Assert.Single(with.Value.Warnings!), StringComparison.Ordinal);
    }
}
