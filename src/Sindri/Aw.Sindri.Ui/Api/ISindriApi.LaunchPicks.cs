using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The model picks read (<see cref="ISindriApi"/>, continued; model-pick spec §6.1).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/launch/picks?item=&amp;instance= (model-pick spec §6.1): the model picks of the runs, oldest first.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="item">Only this item's picks, or null for every item.</param>
    /// <param name="instance">Only this workflow instance's picks (its address), or null for every instance.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The picks, SND-CLI-003 for a malformed item or instance, or SND-UI-002/003.</returns>
    public Task<Result<LaunchPicksView>> LaunchPicks(Uri api, string? item, string? instance, CancellationToken cancellationToken);
}
