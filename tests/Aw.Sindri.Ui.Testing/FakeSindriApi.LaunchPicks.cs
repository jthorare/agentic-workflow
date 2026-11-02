using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The model picks read of the fake (model-pick spec §6.1): answers a golden and records what it was asked.</summary>
internal sealed partial class FakeSindriApi
{
    /// <summary>GET /sindri/launch/picks' answer.</summary>
    public RouteAnswer LaunchPicksAnswer { get; set; } = RouteAnswer.Api("launch-picks", "empty");

    /// <summary>The item and instance of the latest picks read; null before the first.</summary>
    public (string? Item, string? Instance)? LastPicksRead { get; private set; }

    public Task<Result<LaunchPicksView>> LaunchPicks(Uri api, string? item, string? instance, CancellationToken cancellationToken)
    {
        LastPicksRead = (item, instance);
        return Task.FromResult(Answer<LaunchPicksView>(api, SindriApiClient.LaunchPicksRoute(item, instance), LaunchPicksAnswer));
    }
}
