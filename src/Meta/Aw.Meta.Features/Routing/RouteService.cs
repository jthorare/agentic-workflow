using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Features.Routing;

/// <summary>Suggests, and on request writes, which lanes move to another provider.</summary>
/// <param name="config">The team config.</param>
/// <param name="reader">Looks at the lanes and the meta.</param>
/// <param name="standings">Adds provider, model and usage windows.</param>
/// <param name="codexModels">The model names Codex lists, to catch a typo in the routing block.</param>
internal sealed class RouteService(IMetaConfig config, UsageReader reader, LaneStandingReader standings, ICodexModels codexModels) : IService<RouteRequest, RouteResponse>
{
    public async ValueTask<Result<RouteResponse>> Handle(RouteRequest request, CancellationToken cancellationToken)
    {
        var loaded = await config.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var team = loaded.Value;
        if (request.Lane is { } named && !team.Lanes.Contains(named, StringComparer.Ordinal))
        {
            return MetaErrors.UnknownLane.With($"'{named}' is not a lane of team {team.Team}");
        }

        var readings = await reader.Read(team, cancellationToken).ConfigureAwait(false);
        if (readings.IsError)
        {
            return readings.Error;
        }

        var plan = RoutePlanner.Plan(team.Routing, await standings.Read(team, readings.Value, cancellationToken).ConfigureAwait(false));
        var shown = new RouteResponse([.. plan.Suggestions.Where(s => Wanted(request, s.Lane))], [.. plan.Skipped.Where(s => Wanted(request, s.Lane))], Applied: false, Attention: false, await Warnings(team.Routing, cancellationToken).ConfigureAwait(false));
        if (!request.Apply || shown.Suggestions.Count == 0)
        {
            return shown with { Attention = shown.Suggestions.Count > 0 };
        }

        return await Apply(team, shown, request.Force, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<RouteResponse>> Apply(TeamConfig team, RouteResponse shown, bool force, CancellationToken cancellationToken)
    {
        if (!force && shown.Suggestions.Where(static m => !m.TargetKnown).Select(static m => m.ToAgent).Distinct(StringComparer.Ordinal).ToList() is { Count: > 0 } unknown)
        {
            return MetaErrors.Invalid.With($"route --apply: no lane on {string.Join(", ", unknown)} shows a usage reading, so it may be limited too; add --force to apply anyway");
        }

        var lanes = new Dictionary<string, LaneRoute>(team.Routing.Lanes, StringComparer.Ordinal);
        foreach (var move in shown.Suggestions)
        {
            lanes[move.Lane] = new LaneRoute(move.ToAgent, move.ToModel);
        }

        var saved = await config.Save(team with { Routing = team.Routing with { Lanes = lanes } }, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : shown with { Applied = true, Warnings = [.. shown.Warnings ?? [], .. HookReminders(shown.Suggestions)] };
    }

    private async ValueTask<IReadOnlyList<string>> Warnings(RoutingConfig routing, CancellationToken cancellationToken)
    {
        var known = await codexModels.Known(cancellationToken).ConfigureAwait(false);
        return known.IsSuccess && known.Value is { } slugs ? CodexModelCheck.Warnings(routing, slugs) : [];
    }

    // A lane started on another agent only takes part in the obligations through that agent's hooks.
    private static IEnumerable<string> HookReminders(IReadOnlyList<RouteSuggestion> moves) =>
        moves.Select(static m => m.ToAgent).Where(static a => !string.Equals(a, LaneAgent.ClaudeCode, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal)
            .Select(static a => $"hooks: {a} lanes take part through hooks; install them with scripts/install-meta.sh --provider {a} and trust them with /hooks in {a} before the lane starts");

    private static bool Wanted(RouteRequest request, string lane) => request.Lane is null || string.Equals(lane, request.Lane, StringComparison.Ordinal);
}
