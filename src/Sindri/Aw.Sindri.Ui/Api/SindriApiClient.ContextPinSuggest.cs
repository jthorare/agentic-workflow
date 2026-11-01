using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The pin routes (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<PinSuggestView>> ContextPinSuggest(Uri api, PinSuggestQuery query, CancellationToken cancellationToken) =>
        PinSuggestBatches.Run(query, one => Get<PinSuggestView>(api, ContextRoutes.PinSuggest(one), cancellationToken));

    /// <inheritdoc />
    public Task<Result<PinsView>> ContextPins(Uri api, string repo, CancellationToken cancellationToken) =>
        Get<PinsView>(api, ContextRoutes.Pins(repo), cancellationToken);
}
