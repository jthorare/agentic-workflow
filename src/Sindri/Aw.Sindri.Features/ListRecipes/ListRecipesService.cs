using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>
/// The registered recipes, or the stale ones (engine spec §4.1, §6.3 'recipe list [--stale]'; README X34): each with
/// where it stands, how it was approved, why it is stale and its inputs. Stale means approved and either legacy-shape (an unapproved legacy row is marked but not listed here), drifted from
/// this build's blocks or pinning a child that was revoked or superseded. Each line also carries its status word and, read from the ledger, whether it only
/// ever runs as a child and its stats at the request's window (API spec §3.8, E-25): the stats are left out for a draft and for a child-only recipe.
/// </summary>
/// <param name="registry">Reads where the registered recipes stand.</param>
/// <param name="rows">The ledger's workflow rows; null serves the lines without stats.</param>
/// <param name="clock">The read's time.</param>
internal sealed class ListRecipesService(RecipeRegistryReader registry, IWorkflowStatRows? rows = null, TimeProvider? clock = null) : IService<ListRecipesRequest, ListRecipesResponse>
{
    public async ValueTask<Result<ListRecipesResponse>> Handle(ListRecipesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var window = StatsWindow.Parse(request.Window);
        if (window.IsError)
        {
            return window.Error;
        }

        var lines = await registry.Summaries(cancellationToken).ConfigureAwait(false);
        if (lines.IsError)
        {
            return lines.Error;
        }

        var kept = lines.Value.Where(l => !request.Stale || l is { Standing: RecipeStanding.Approved, Stale.Count: > 0 }).ToList();
        if (rows is null)
        {
            return new ListRecipesResponse([.. kept.Select(WithStatus)]);
        }

        var children = await registry.ChildRecipeIds(cancellationToken).ConfigureAwait(false);
        var read = children.IsError ? children.Error : await rows.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        return new ListRecipesResponse([.. WithStats(kept, children.Value, read.Value, window.Value, now)], window.Value.Text);
    }

    private static IEnumerable<RecipeSummary> WithStats(List<RecipeSummary> kept, IReadOnlySet<RecipeId> children, IReadOnlyList<StatEvent> rows, StatsWindow window, DateTimeOffset now)
    {
        var counted = new Dictionary<RecipeId, RecipeListStats?>();
        return kept.Select(line =>
        {
            var childOnly = children.Contains(line.Id) && !RecipeStatsCounter.HasRoot(rows, line.Id);
            if (!counted.TryGetValue(line.Id, out var stats))
            {
                counted[line.Id] = stats = childOnly ? null : RecipeListStats.Of(RecipeStatsCounter.Count(rows, line.Id, hashPrefix: null, window, now));
            }

            return WithStatus(line) with { ChildOnly = childOnly, Stats = line.Standing == RecipeStanding.Unapproved || childOnly ? null : stats };
        });
    }

    private static RecipeSummary WithStatus(RecipeSummary line)
    {
        var status = RecipeStatus.Of(line);
        return line with { Status = status.Word, StatusDetail = status.Detail };
    }
}
