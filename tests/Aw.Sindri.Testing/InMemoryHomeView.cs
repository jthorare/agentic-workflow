using Aw.Kernel;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Testing;

/// <summary>The Home view in memory (<see cref="IHomeViewReader"/>): the rows a test puts in, paged by the same rule as the real view (<see cref="HomePaging"/>).</summary>
public sealed class InMemoryHomeView : IHomeViewReader
{
    private readonly Dictionary<string, HomeRow> _rows = new(StringComparer.Ordinal);

    /// <summary>Puts <paramref name="row"/> in the view, replacing the row with its id.</summary>
    /// <param name="row">The row.</param>
    public void Put(HomeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _rows[row.Id.Value] = row;
    }

    /// <summary>Removes the row under <paramref name="id"/>, if any.</summary>
    /// <param name="id">The row's id.</param>
    public void Remove(HomeRowId id) => _ = _rows.Remove(id.Value);

    /// <inheritdoc />
    public ValueTask<Result<HomePage>> List(HomeQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<HomePage>>(HomePaging.Page([.. _rows.Values], query));

    /// <inheritdoc />
    public ValueTask<Result<HomeRow?>> Get(HomeRowId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<HomeRow?>>(_rows.GetValueOrDefault(id.Value));
}
