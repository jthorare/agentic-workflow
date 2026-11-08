using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>
/// What a job's start checks inside the gate when the check needs scoped services (the approved profile, the services this build has), and which
/// background service the job runs, if any (RunService: its start event, its work's caller and its record then carry that service). Registered
/// scoped per request type; a job without one needs none.
/// </summary>
/// <typeparam name="TRequest">The job request.</typeparam>
public interface IJobAdmission<in TRequest>
{
    /// <summary>Checks <paramref name="request"/> before the start is accepted.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The service the job runs (or null), or the error that refuses the start.</returns>
    public ValueTask<Result<ServiceName?>> Admit(TRequest request, CancellationToken cancellationToken);

    /// <summary>Gives back whatever <see cref="Admit"/> took when the start is refused after the admission (the claim was lost, the start could not be recorded). The default takes nothing.</summary>
    /// <param name="request">The request that was admitted.</param>
    public void Rollback(TRequest request)
    {
    }
}
