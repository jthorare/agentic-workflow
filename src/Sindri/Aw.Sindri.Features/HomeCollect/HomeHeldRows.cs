using Aw.Kernel;
using Aw.Sindri.Core.Home;

namespace Aw.Sindri.Features.HomeCollect;

/// <summary>The rows Home holds for one collector: every row of the view whose source is the collector's and whose kind it raises, in any state, read page by page.</summary>
internal static class HomeHeldRows
{
    // why: the view pages at 200 rows; a cursor that vanished between pages starts at the top again, so the walk is capped instead of trusted to end
    private const int MaxPages = 50;

    private static readonly HomeState[] _allStates = [HomeState.Open, HomeState.Snoozed, HomeState.Done, HomeState.Muted];

    /// <summary>The held rows of <paramref name="source"/> whose kind is one of <paramref name="kinds"/>.</summary>
    /// <param name="view">The Home view.</param>
    /// <param name="source">The collector's source name.</param>
    /// <param name="kinds">The kinds it raises.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows, each once.</returns>
    public static async ValueTask<Result<IReadOnlyList<HomeRow>>> Of(IHomeViewReader view, string source, IReadOnlySet<HomeKind> kinds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(kinds);
        var found = new Dictionary<string, HomeRow>(StringComparer.Ordinal);
        string? after = null;
        for (var page = 0; page < MaxPages; page++)
        {
            var read = await view.List(new HomeQuery(Group: null, _allStates, after, HomePaging.MaxLimit), cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            foreach (var row in read.Value.Rows.Where(r => kinds.Contains(r.Kind) && string.Equals(r.Source.Name, source, StringComparison.Ordinal)))
            {
                found[row.Id.Value] = row;
            }

            if (read.Value.Cursor is not { } next)
            {
                break;
            }

            after = next;
        }

        return Results.Success<IReadOnlyList<HomeRow>>([.. found.Values]);
    }
}
