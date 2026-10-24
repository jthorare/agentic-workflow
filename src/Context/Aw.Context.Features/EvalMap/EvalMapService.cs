using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EvalMap;

/// <summary>
/// The map's quality on the owner's own accept and reject decisions (spec §9.2 item 2): node precision per provenance and link precision (accepted over
/// accepted plus rejected; a proposal nobody decided is counted apart, never as a miss), and the unmapped rate, the files changed in the 90 days before the
/// mapped commit (not before today) that implement no node. A human or service call; no model is called.
/// </summary>
internal sealed class EvalMapService(MapReader reader, IMergeHistory history, IEvalStore store, TimeProvider clock) : IService<MapQualityRequest, MapQualityRun>
{
    private static readonly TimeSpan _window = TimeSpan.FromDays(90);

    public async ValueTask<Result<MapQualityRun>> Handle(MapQualityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var map = await reader.Read(request.Scope, draft: false, cancellationToken).ConfigureAwait(false);
        if (map.IsError)
        {
            return map.Error;
        }

        var decided = Decisions(map.Value.Snapshot);
        var unmapped = await Unmapped(request.Scope, map.Value, cancellationToken).ConfigureAwait(false);
        if (unmapped.IsError)
        {
            return unmapped.Error;
        }

        var report = Report(map.Value, decided, unmapped.Value, clock.GetUtcNow());
        var written = await store.WriteReport(EvalReportKind.Map, report, report.CreatedAt, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new MapQualityRun(report, written.Value);
    }

    private static MapQualityReport Report(VisibleMap map, Decided decided, (Ratio Rate, string? Window) unmapped, DateTimeOffset now)
    {
        var graph = map.Snapshot.Graph;
        var (det, model) = (Count(decided.Nodes, graph, Provenance.Deterministic), Count(decided.Nodes, graph, Provenance.Model));
        var total = new Ratio(det.Hits + model.Hits, det.Total + model.Total);
        var proposed = graph.Nodes.Where(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision) && n.Provenance is Provenance.Deterministic or Provenance.Model).Select(n => n.Key.Value).ToList();
        var undecided = proposed.Count(k => !decided.Nodes.ContainsKey(k));
        var links = new Ratio(decided.Links.Values.Count(a => a == OverrideAction.Accept), decided.Links.Count);
        return new MapQualityReport(MapQualityReport.CurrentSchemaVersion, now, map.Snapshot.Tag, total, det, model, links, undecided, unmapped.Rate, unmapped.Window,
            MapQualityBars.Judge(total, unmapped.Rate));
    }

    private static Ratio Count(Dictionary<string, OverrideAction> nodes, GraphSnapshot graph, Provenance kind)
    {
        var rows = nodes.Where(d => graph.Nodes.FirstOrDefault(n => string.Equals(n.Key.Value, d.Key, StringComparison.Ordinal)) is { } node && node.Provenance == kind).ToList();
        return new Ratio(rows.Count(r => r.Value == OverrideAction.Accept), rows.Count);
    }

    // The latest active accept or reject per node, and per link.
    private static Decided Decisions(MapSnapshot snapshot)
    {
        var nodes = new Dictionary<string, OverrideAction>(StringComparer.Ordinal);
        var links = new Dictionary<string, OverrideAction>(StringComparer.Ordinal);
        foreach (var o in OverrideLog.Active(snapshot.Overrides).Where(o => o.Action is OverrideAction.Accept or OverrideAction.Reject))
        {
            if (o.Link is { } link)
            {
                links[link.Value] = o.Action;
            }
            else
            {
                nodes[o.Target.Value] = o.Action;
            }
        }

        return new Decided(nodes, links);
    }

    private async ValueTask<Result<(Ratio Rate, string? Window)>> Unmapped(ContextScope scope, VisibleMap map, CancellationToken cancellationToken)
    {
        var none = (new Ratio(0, 0), (string?)null);
        var version = map.Snapshot.Version;
        var repos = SettingsReader.Read(scope.Settings).Bind(s => ScopeRules.Resolve(s, scope.Repos, scope.Caller));
        var repo = repos.IsError ? null : repos.Value.FirstOrDefault(r => r.Name == version.Repo);
        if (repo is null || version.Commit is not { Length: > 0 } commit)
        {
            return none;
        }

        var log = await history.History(repo, cancellationToken).ConfigureAwait(false);
        if (log.IsError)
        {
            return log.Error;
        }

        if (log.Value.Commits.FirstOrDefault(c => c.Sha.StartsWith(commit, StringComparison.Ordinal)) is not { } at)
        {
            return none;
        }

        var changed = log.Value.Commits.Where(c => c.IsMerge && c.At <= at.At && c.At >= at.At - _window).SelectMany(c => c.ChangedFiles).Where(f => !repo.Denies(f)).Distinct(StringComparer.Ordinal).ToList();
        var mapped = map.Graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Kind == NodeKind.Code).Select(e => e.From.Value["code:".Length..]).ToHashSet(StringComparer.Ordinal);
        var window = string.Create(CultureInfo.InvariantCulture, $"{at.At - _window:yyyy-MM-dd}..{at.At:yyyy-MM-dd}");
        return (new Ratio(changed.Count(f => !mapped.Contains(f)), changed.Count), window);
    }

    private sealed record Decided(Dictionary<string, OverrideAction> Nodes, Dictionary<string, OverrideAction> Links);
}
