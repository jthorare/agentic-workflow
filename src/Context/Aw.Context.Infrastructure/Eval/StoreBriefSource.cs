using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// Briefs from the Sindri-owned record store: the stored Linear issues whose tracker key ('TEAM-n', read from the issue's stored linear.app permalink) names one of the profile's teams, paged through the store so no team's listing is capped. A brief is the
/// stored title and the stored chunks, cleaned again at the ingest boundary. The store keeps no content history (a record holds its latest
/// text under its latest 'updatedAt'), so an issue edited after its creation is leaky, as any one-record-per-issue store does; the corpus
/// labels it from the issue's two dates. It is the eval harness's read, not an arm.
/// </summary>
internal sealed class StoreBriefSource(IRecordStore store) : IBriefSource
{
    private const int PageSize = 1000;   // the store's own page limit
    private static readonly RecordFilter _issues = new(LinearSourceIds.Issues, Repo: null, RecordKind.Issue);

    public async ValueTask<Result<BriefListing>> Issues(BriefScope scope, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Settings.LinearTeams.Count == 0)
        {
            return NoSource();
        }

        var teams = scope.Settings.LinearTeams.ToHashSet(StringComparer.Ordinal);
        var issues = new List<BriefIssue>();
        RecordKey? after = null;
        while (true)
        {
            var page = await store.List(_issues, after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return Unavailable(page.Error);
            }

            if (page.Value.Count == 0)
            {
                break;
            }

            after = page.Value[^1].Key;
            issues.AddRange(page.Value.Select(r => Issue(r, teams, since, scope)).OfType<BriefIssue>());
        }

        return new BriefListing([.. issues.OrderBy(i => i.Key, StringComparer.Ordinal)], []);   // a paged read has no cap
    }

    public async ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(issue);
        if (!RecordKey.TryFrom(issue.RecordRef, out var key))
        {
            return Results.Success<Brief?>(value: null);
        }

        var record = await store.Get(key, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return Unavailable(record.Error);
        }

        if (record.Value is not { RemovedAt: null, Blocked: false })
        {
            return Results.Success<Brief?>(value: null);   // gone or withheld: the sample is dropped
        }

        var chunks = await store.Chunks(key, cancellationToken).ConfigureAwait(false);
        if (chunks.IsError)
        {
            return Unavailable(chunks.Error);
        }

        var text = string.Join('\n', chunks.Value.OrderBy(c => c.Seq).Select(c => c.Text));
        var clean = scope.Scrubbers.Ingest.Clean(issue.Title + "\n\n" + text, singleLine: false);
        return Results.Success<Brief?>(new Brief(issue.Key, clean.Blocked ? string.Empty : clean.Text));
    }

    private static BriefIssue? Issue(StoredRecord record, HashSet<string> teams, DateTimeOffset? since, BriefScope scope)
    {
        if (record.RemovedAt is not null || record.Blocked || record.CreatedAt is not { } created)
        {
            return null;   // a tombstone, withheld text or no creation time (no cut): not a sample
        }

        var updated = record.UpdatedAt ?? created;
        if (LinearIdentifiers.FromPermalink(record.Url, teams) is not { } key || (since is { } start && updated < start))
        {
            return null;
        }

        var title = scope.Scrubbers.Ingest.Clean(record.Title, singleLine: true);
        return new BriefIssue(key, record.Key.Value, record.Version.Value, created, updated, title.Blocked ? string.Empty : $"[{key}] {title.Text}");
    }

    private static Error Unavailable(Error cause) =>
        ContextErrors.AllSourcesUnavailable.With($"the brief source can't be read ({cause.Code}); fix: sindri context sources status");

    private static Error NoSource() =>
        ContextErrors.NoBriefSource.With("no configured source can supply issue briefs: needs 'context.sources.linear.teams' in the approved profile (fix: sindri context sources status)");
}
