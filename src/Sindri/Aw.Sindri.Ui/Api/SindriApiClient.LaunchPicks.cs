using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The model picks read (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <summary>The path of GET /sindri/launch/picks.</summary>
    internal const string LaunchPicksPath = "/sindri/launch/picks";

    /// <inheritdoc />
    public Task<Result<LaunchPicksView>> LaunchPicks(Uri api, string? item, string? instance, CancellationToken cancellationToken) =>
        Get<LaunchPicksView>(api, LaunchPicksRoute(item, instance), cancellationToken, view: true);

    /// <summary>The path with its query: only the filters that are set, escaped.</summary>
    /// <param name="item">The item, or null.</param>
    /// <param name="instance">The instance address, or null.</param>
    /// <returns>The path.</returns>
    internal static string LaunchPicksRoute(string? item, string? instance)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(item))
        {
            query.Add("item=" + Uri.EscapeDataString(item));
        }

        if (!string.IsNullOrEmpty(instance))
        {
            query.Add("instance=" + Uri.EscapeDataString(instance));
        }

        return query.Count == 0 ? LaunchPicksPath : LaunchPicksPath + "?" + string.Join('&', query);
    }
}
