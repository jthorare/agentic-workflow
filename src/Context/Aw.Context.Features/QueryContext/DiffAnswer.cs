using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The diff shape's anchors (S-D): the repo the range is read in (one answer for every reason the repo is out of reach, 045), the map the caller
/// may read (030, which a session whose own repo has no map nodes also gets, or 044 for a draft that isn't there), the repo's changes from the merge base (110, 111, 048), then the changed files against
/// the map (<see cref="DiffAnchors"/>) and the cascade from the nodes they implement. Every caller's paths are scrubbed like the map text of the same answer.
/// </summary>
/// <param name="graphs">The map reader.</param>
/// <param name="repos">The repo reader.</param>
internal sealed class DiffAnswer(MapReader graphs, IRepoReader repos)
{
    internal async ValueTask<Result<(GraphPart Graph, IReadOnlyList<string> Terms)>> Build(QueryContextRequest request, DiffRange diff, MapRead read, (ContextSettings Settings, Scrubbers Scrubbers) context,
        CancellationToken cancellationToken)
    {
        var query = request.Query;
        var repo = Addressed(context.Settings, request.Scope, query.Repo);
        var map = repo.IsError ? repo.Error : await graphs.ReadViewAt(request.Scope, read, cancellationToken).ConfigureAwait(false);
        var changes = map.IsError ? map.Error : await repos.Changes(repo.Value, diff, cancellationToken).ConfigureAwait(false);
        if (repo.IsError || map.IsError || changes.IsError)
        {
            return repo.IsError ? repo.Error : map.IsError ? map.Error : changes.Error;
        }

        var (part, anchors) = DiffAnchors.Build(changes.Value, repo.Value, map.Value.Graph);
        var text = new MapText(context.Scrubbers);
        var shown = Scrubbed(part, text);
        var terms = anchors.SelectMany(a => text.SearchTerms(a.Label)).Distinct(StringComparer.Ordinal).Take(QueryRules.MaxTerms).ToList();
        return (GraphAnswer.Build(map.Value, anchors, [], CascadeReach.Of(query, context.Settings), text) with { Diff = shown }, terms);
    }

    // Changed paths are repo text: every caller reads them through the same egress scrub the cascade's code paths get (MapText), so one answer never shows a path two ways.
    private static DiffPart Scrubbed(DiffPart part, MapText text) =>
        part with { Files = [.. part.Files.Select(f => f with { Path = text.Line(f.Path) })], Unmapped = [.. part.Unmapped.Select(text.Line)] };

    private static Result<ContextRepo> Addressed(ContextSettings settings, ContextScope scope, ContextRepoName? name)
    {
        var inScope = ScopeRules.Resolve(settings, scope.Repos, scope.Caller, scope.LeftOut);
        if (inScope.IsError)
        {
            return inScope.Error;
        }

        var found = name is { } wanted ? inScope.Value.FirstOrDefault(r => r.Name == wanted) : inScope.Value.Count == 1 ? inScope.Value[0] : null;
        if (found is not null)
        {
            return found;
        }

        return name is null && inScope.Value.Count > 1
            ? ContextErrors.QueryInvalid.With($"name the repo the range belongs to (repos: {string.Join(", ", inScope.Value.Select(r => r.Name.Value))})")
            : ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can read (fix: name a repo of the profile; a session names its own X-Sindri-Repo)");
    }
}
