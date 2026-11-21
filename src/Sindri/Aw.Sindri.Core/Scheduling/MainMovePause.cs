using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>Whether code-index-main-move, the service that starts every code index build, can start one. The one read behind the map outlooks, the Health card and doctor (#3399).</summary>
public static class MainMovePause
{
    /// <summary>The service's name.</summary>
    public static ServiceName Service { get; } = ServiceName.From("code-index-main-move");

    /// <summary>True unless a human paused the service.</summary>
    /// <param name="pauses">The paused services.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether the service runs.</returns>
    public static async ValueTask<bool> Active(IServicePauses pauses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pauses);

        // why: a pause read that fails (the views are rebuilding) counts as active; reading it as paused would freeze a map and hide a build that is coming
        var paused = await pauses.Paused(cancellationToken).ConfigureAwait(false);
        return paused.IsError || !paused.Value.Contains(Service);
    }
}
