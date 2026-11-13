using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The <see cref="ILaunchProbes"/> over the ledger. The key carries the sequence of the probe it replaces, so a state that comes back (A, B, A) is a new
/// fact, and two probes that race over the same predecessor record one.
/// </summary>
/// <param name="launcher">Probes the viewer, the host and the catalog.</param>
/// <param name="approvals">The approved profile, which names the repos.</param>
/// <param name="locator">Says whether a repo's path is a checkout.</param>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class LaunchProbeRecorder(ILauncher launcher, IApprovedProfile approvals, IRepoLocator locator, ILedgerStore ledger, EventFactory events) : ILaunchProbes
{
    /// <inheritdoc/>
    public async ValueTask<Result<RecordedProbe>> Record(LeaseGrant grant, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var latest = await ledger.Latest(EventTypes.LaunchProbed, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var stopped = approved.IsError && string.Equals(approved.Error.Code, SindriErrors.ProfileInvalid.Code, StringComparison.Ordinal);
        if (approved.IsError && !stopped)
        {
            return approved.Error;
        }

        var probed = stopped ? await Stopped(latest.Value, approved.Error.Message, cancellationToken).ConfigureAwait(false) : await Probed(approved.Value, cancellationToken).ConfigureAwait(false);
        var entry = events.Entry(EventTypes.LaunchProbed, probed, string.Create(CultureInfo.InvariantCulture, $"{EventTypes.LaunchProbed}:{latest.Value?.Sequence ?? 0}"));
        if (latest.Value is { } last && string.Equals(last.Envelope.Payload, entry.Envelope.Payload, StringComparison.Ordinal))
        {
            return new RecordedProbe(probed, Changed: false);
        }

        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new RecordedProbe(probed, Changed: !appended.Value.AlreadyRecorded);
    }

    private async ValueTask<LaunchProbed> Probed(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var found = await launcher.Probe(cancellationToken).ConfigureAwait(false);
        return found with { Repos = await Repos(approved?.Profile.Repos ?? [], cancellationToken).ConfigureAwait(false) };
    }

    // An invalid profile file stops probing (#3594): the last probe's facts stay and carry why; with no probe yet, one probe with no repos gives the facts to keep.
    private async ValueTask<LaunchProbed> Stopped(LedgerEvent? last, string reason, CancellationToken cancellationToken)
    {
        var kept = last is null ? null : events.Payload<LaunchProbed>(last);
        var facts = kept is { IsSuccess: true } ? kept.Value : (await launcher.Probe(cancellationToken).ConfigureAwait(false)) with { Repos = [] };
        return facts with { Stopped = $"profile file invalid: {reason}" };
    }

    private async ValueTask<IReadOnlyList<RepoFacts>> Repos(IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken)
    {
        var facts = new List<RepoFacts>(repos.Count);
        foreach (var repo in repos)
        {
            var located = await locator.Locate(repo.Path, repos, cancellationToken).ConfigureAwait(false);
            facts.Add(new RepoFacts(RepoName.From(repo.Name), located is { } name && string.Equals(name.Value, repo.Name, StringComparison.Ordinal)));
        }

        return facts;
    }
}
