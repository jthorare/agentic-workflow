using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The pin-suggest route (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<PinSuggestView>> ContextPinSuggest(Uri api, PinSuggestQuery query, CancellationToken cancellationToken) =>
        PinSuggestBatches.Run(query, one => Get<PinSuggestView>(api, ContextRoutes.PinSuggest(one), cancellationToken));
}
