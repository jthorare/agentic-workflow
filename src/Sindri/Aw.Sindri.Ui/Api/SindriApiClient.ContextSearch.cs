using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<MapSearchView>> ContextSearch(Uri api, MapSearchQuery query, CancellationToken cancellationToken) =>
        Get<MapSearchView>(api, ContextRoutes.Search(query), cancellationToken);
}
