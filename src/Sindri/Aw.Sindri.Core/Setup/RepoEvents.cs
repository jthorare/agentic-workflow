using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Setup;

/// <summary>Records 'repo.added' (setup-once spec §5.1, §7.5): the repo's name and path, fenced by the lease this process holds, carrying the running command's id.</summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller whose command id the event carries.</param>
public sealed class RepoEvents(HeldLease held, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers)
{
    /// <summary>Whether an event can be recorded now: this process holds the lease and a call is running. A job checks it before it changes anything.</summary>
    public bool CanRecord() => held.Current is not null && callers.Current is not null;

    /// <summary>The idempotency key of a repo's 'repo.added': one per repo name, so the add job and the backfill can never both record it.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <returns>'repo.added:&lt;name&gt;'.</returns>
    public static string KeyOf(RepoName repo) => "repo.added:" + repo.Value;

    /// <summary>Appends 'repo.added' under <see cref="KeyOf"/>; recording it again is a no-op.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="path">Its checkout (kept in the non-public payload).</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or SND-API-001 when this process holds no lease or no call is running, or the ledger's error.</returns>
    public async ValueTask<Result<Unit>> Added(RepoName repo, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await ledger.Append(events.Entry(EventTypes.RepoAdded, new RepoAdded(caller.CommandId, repo, path), KeyOf(repo)), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }

    /// <summary>The idempotency key of a repo's 'repo.onboarded': one per repo and approved hash, so a tick and the OnboardRepo job cannot both record it.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="approved">The approved profile that names it.</param>
    /// <returns>'repo.onboarded:&lt;name&gt;:&lt;hash&gt;'.</returns>
    public static string OnboardedKeyOf(RepoName repo, ProfileHash approved) => $"repo.onboarded:{repo.Value}:{approved.Value}";

    /// <summary>Appends 'repo.onboarded' under <see cref="OnboardedKeyOf"/>; recording it again is a no-op.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="approved">The approved profile that names it.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or SND-API-001 when this process holds no lease or no call is running, or the ledger's error.</returns>
    public async ValueTask<Result<Unit>> Onboarded(RepoName repo, ProfileHash approved, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await ledger.Append(events.Entry(EventTypes.RepoOnboarded, new RepoOnboarded(caller.CommandId, repo, approved), OnboardedKeyOf(repo, approved)), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
