using System.Globalization;
using Aw.Kernel;
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
        var found = await launcher.Probe(cancellationToken).ConfigureAwait(false);
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var probed = found with { Repos = await Repos(approved.Value?.Profile.Repos ?? [], cancellationToken).ConfigureAwait(false) };
        var latest = await ledger.Latest(EventTypes.LaunchProbed, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var entry = events.Entry(EventTypes.LaunchProbed, probed, string.Create(CultureInfo.InvariantCulture, $"{EventTypes.LaunchProbed}:{latest.Value?.Sequence ?? 0}"));
        if (latest.Value is { } last && string.Equals(last.Envelope.Payload, entry.Envelope.Payload, StringComparison.Ordinal))
        {
            return new RecordedProbe(probed, Changed: false);
        }

        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new RecordedProbe(probed, Changed: !appended.Value.AlreadyRecorded);
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
