using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Binds the job and service routes' raw values (P1 binding rule): a '{jobId}' segment, a '{name}' segment and the '?state=' and '?kind=' filters, each a 400 SND-CLI-003 when malformed.</summary>
internal static class JobRoute
{
    /// <summary>The job in a '{jobId}' segment.</summary>
    /// <param name="jobId">The segment.</param>
    /// <returns>The id, or SND-CLI-003.</returns>
    internal static Result<JobId> Job(string jobId) =>
        JobId.TryParse(jobId, provider: null, out var id) ? id : SindriErrors.InvalidInput.With("'" + Echo(jobId) + "' isn't a job id (JOB-…)");

    /// <summary>The service in a '{name}' segment.</summary>
    /// <param name="name">The segment.</param>
    /// <returns>The name, or SND-CLI-003.</returns>
    internal static Result<ServiceName> Service(string name) =>
        ServiceName.TryParse(name, provider: null, out var service) ? service : SindriErrors.InvalidInput.With("'" + Echo(name) + "' isn't a service name");

    /// <summary>The '?state=' filter, or null when absent.</summary>
    /// <param name="state">The query value.</param>
    /// <returns>The state, or SND-CLI-003.</returns>
    internal static Result<JobState?> State(string? state) => state switch
    {
        null => (JobState?)null,
        "running" => JobState.Running,
        "succeeded" => JobState.Succeeded,
        "failed" => JobState.Failed,
        "cancelled" => JobState.Cancelled,
        "waiting" => JobState.Waiting,
        _ => SindriErrors.InvalidInput.With("'" + Echo(state) + "' isn't a job state (running, waiting, succeeded, failed or cancelled)"),
    };

    /// <summary>The '?kind=' filter, or null when absent.</summary>
    /// <param name="kind">The query value.</param>
    /// <returns>The kind, or SND-CLI-003.</returns>
    internal static Result<JobKind?> Kind(string? kind)
    {
        if (kind is null)
        {
            return (JobKind?)null;
        }

        return JobKind.TryFrom(kind, out var parsed) ? parsed : SindriErrors.InvalidInput.With("'" + Echo(kind) + "' isn't a job kind");
    }

    private static string Echo(string value) => SafeText.Line(SafeText.Escape(value), 80);
}
