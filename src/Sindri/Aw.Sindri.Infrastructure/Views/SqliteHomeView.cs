using Aw.Kernel;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The Home page's read port over the 'home-rows' view (<see cref="IHomeViewReader"/>): rows read through the views reader, paged by <see cref="HomePaging"/>.</summary>
/// <param name="views">The views reader; it never creates views.db, so a box with no views yet has an empty Home.</param>
internal sealed class SqliteHomeView(IViewReader views) : IHomeViewReader
{
    public async ValueTask<Result<HomePage>> List(HomeQuery query, CancellationToken cancellationToken)
    {
        var rows = await views.List<HomeRow>(ViewNames.HomeRows, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        return HomePaging.Page(rows.Value, query);
    }

    public async ValueTask<Result<HomeRow?>> Get(HomeRowId id, CancellationToken cancellationToken) =>
        await views.Get<HomeRow>(ViewNames.HomeRows, id.Value, cancellationToken).ConfigureAwait(false);
}
