using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The re-run rule of AddRepo (setup-once spec §5.1): a profile entry whose 'repo.added' is missing (a crash between the profile write and the event) gets the event appended. The ledger is searched first, so a repo added
/// the ordinary way is never given a second one, and the event has the same key as the add job's (one per repo name), so a tick or another add landing between the profile write and the job's append cannot give a repo two. A repo that has its event is remembered, so the setup service's tick does not read the ledger for it again.
/// </summary>
/// <param name="recorder">Appends the event.</param>
/// <param name="reader">Looks for earlier ones.</param>
/// <param name="events">Reads their payloads.</param>
/// <param name="git">Forgets what it knows of the repos, since one is new to it.</param>
/// <param name="known">The repos known to have their event.</param>
public sealed class RepoAddedBackfill(RepoEvents recorder, ILedgerReader reader, EventFactory events, RepoGitCache git, KnownRepoEvents known)
{
    private const int Page = 500;
    private static readonly LedgerFilter _filter = new(TypeFilter.Parse(EventTypes.RepoAdded).Value, Item: null);

    /// <summary>Appends 'repo.added' for each of <paramref name="entries"/> that the ledger has none for.</summary>
    /// <param name="entries">Profile entries.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that ends when the events are there.</returns>
    public async ValueTask Ensure(IReadOnlyList<RepoEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var missing = entries.Where(e => !known.Has(e.Name) && RepoName.TryFrom(e.Name, out _)).ToList();
        if (missing.Count == 0 || await Recorded(cancellationToken).ConfigureAwait(false) is not { } recorded)
        {
            return;
        }

        foreach (var entry in missing)
        {
            if (!recorded.Contains(entry.Name) && !await Append(entry, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            known.Add(entry.Name);
        }
    }

    /// <summary>Appends 'repo.added' for <paramref name="entry"/> unless the ledger has one.</summary>
    /// <param name="entry">A profile entry.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that ends when the event is there.</returns>
    public ValueTask Ensure(RepoEntry entry, CancellationToken cancellationToken) => Ensure([entry], cancellationToken);

    private async ValueTask<bool> Append(RepoEntry entry, CancellationToken cancellationToken)
    {
        var appended = await recorder.Added(RepoName.From(entry.Name), entry.Path, cancellationToken).ConfigureAwait(false);
        if (appended.IsSuccess)
        {
            git.Invalidate();
        }

        return appended.IsSuccess;
    }

    // The names the ledger has a 'repo.added' for; null when it could not be read, so nothing is written.
    private async ValueTask<HashSet<string>?> Recorded(CancellationToken cancellationToken)
    {
        var head = await reader.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return null;
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        for (var cursor = 0L; cursor < head.Value;)
        {
            var scan = await reader.Scan(cursor, Page, _filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return null;
            }

            names.UnionWith(scan.Value.Events.Select(events.Payload<RepoAdded>).Where(static p => p.IsSuccess).Select(static p => p.Value.Repo.Value));
            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        return names;
    }
}
