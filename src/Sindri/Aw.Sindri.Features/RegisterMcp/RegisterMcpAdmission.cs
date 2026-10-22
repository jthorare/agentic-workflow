using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>
/// The cheap check a RegisterMcp start makes inside the gate (setup-once spec §5.4): the repo is named by the approved profile snapshot, never the live file (SND-SETUP-004 otherwise), so the code is a real
/// HTTP error before any job is claimed. It keeps the repo it resolved for the job key and the work, keyed by the request itself.
/// </summary>
/// <param name="approvals">The approved profile snapshot.</param>
/// <param name="resolved">Keeps what was worked out for the job key and the work (the singleton the job registration shares).</param>
internal sealed class RegisterMcpAdmission(IApprovedProfile approvals, ResolvedMcpRepos? resolved = null) : IJobAdmission<RegisterMcpJobRequest>
{
    private readonly ResolvedMcpRepos _resolved = resolved ?? new ResolvedMcpRepos();

    public async ValueTask<Result<ServiceName?>> Admit(RegisterMcpJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var repos = approved.Value?.Profile.Repos ?? [];
        var entry = request.Repo is { } named
            ? repos.FirstOrDefault(r => string.Equals(r.Name, named, StringComparison.Ordinal))
            : repos.Count == 1 ? repos[0] : null;
        if (entry is null || !RepoName.TryFrom(entry.Name, out var name))
        {
            return SindriErrors.SetupRepoNotInProfile.With(request.Repo is { } asked
                ? $"'{asked}' is not a repo the approved profile names (fix: add the repo, approve the profile, then try again)"
                : "name the repo (fix: pass repo, the approved profile names none or several)");
        }

        _resolved.Keep(request, name);
        return (ServiceName?)null;
    }

    public void Rollback(RegisterMcpJobRequest request) => _resolved.Drop(request);

    /// <summary>The job key of a start the admission accepted.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The key.</returns>
    public JobKey KeyOf(RegisterMcpJobRequest request) => _resolved.KeyOf(request);

    /// <summary>Takes the repo the admission resolved, once.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The repo, or null when the request was not admitted.</returns>
    public RepoName? Take(RegisterMcpJobRequest request) => _resolved.Take(request);
}
