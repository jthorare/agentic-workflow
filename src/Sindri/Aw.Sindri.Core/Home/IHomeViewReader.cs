using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Home;

/// <summary>The read port over the Home view (home spec §8): a page of rows in ranked order, and one row by id.</summary>
public interface IHomeViewReader
{
    /// <summary>One page of rows: the rows of <paramref name="query"/>'s groups and states in ranked order (position, then id), after its cursor, with the counts over the whole view.</summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page; an empty view gives an empty page.</returns>
    public ValueTask<Result<HomePage>> List(HomeQuery query, CancellationToken cancellationToken);

    /// <summary>The row under <paramref name="id"/>, in any state.</summary>
    /// <param name="id">The row's id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, or null when the view holds none.</returns>
    public ValueTask<Result<HomeRow?>> Get(HomeRowId id, CancellationToken cancellationToken);
}
