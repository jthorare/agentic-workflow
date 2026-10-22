namespace Aw.Sindri.Core.Launch;

/// <summary>What <c lang="bash">sindri serve</c> does to the sessions it launched when it stops: they end, so none outlives the process that supervises it.</summary>
public interface ILaunchShutdown
{
    /// <summary>Ends every session this process launched on the Sindri-owned host and records each as ended.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many sessions were ended.</returns>
    public ValueTask<int> StopAll(CancellationToken cancellationToken);
}
