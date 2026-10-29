using Aw.Context.Core;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// <see cref="IIndexSync"/> over the record feeds (owned-index spec §5.5, §5.8): the same feeds and the same per-source tick as <see cref="SyncSourcesService"/>, without the
/// intent walk or the docs refresh. The feeds tick concurrently, at most <see cref="MaxConcurrentFeeds"/> at once, each under its own time cap (<see cref="FeedCap"/>): a feed that hits it is a capped row, never a failure, and the next tick resumes it. A source that is down is its row's code; the last tick's end and code are kept in the record store (`source_cursor`, repo 'records'), so every process reads the same and they are null only until a source's first tick.
/// </summary>
/// <param name="feeds">The feeds and their lock and last outcomes.</param>
/// <param name="store">The record store the positions are read from.</param>
/// <param name="scrubberFactory">Builds the call's scrubbers.</param>
/// <param name="decisions">The decision pass <see cref="Decide"/> runs; null: none.</param>
/// <param name="steps">The other steps after a feed's tick (the Linear viewer after the projects feed); null: none. They run once the source's lock is released.</param>
/// <param name="synthesis">The concept synthesis <see cref="Decide"/> runs after the per-record pass; null: none.</param>
/// <param name="concepts">The synthesized concepts <see cref="ConceptStatus"/> counts; null: none are kept.</param>
internal sealed class IndexSync(RecordFeedSync feeds, IRecordStore store, IScrubberFactory scrubberFactory, FeedDecisions? decisions = null, TickSteps? steps = null, ConceptSynthesisRun? synthesis = null, IConceptSynthesisStore? concepts = null) : IIndexSync
{
    /// <summary>The longest one feed's tick runs; the index tick comes every 5 minutes, and this sits above one normal Linear or Slack page with its rate-limit sleeps.</summary>
    internal static readonly TimeSpan FeedCap = TimeSpan.FromMinutes(4);

    /// <summary>The most feeds that tick at the same time.</summary>
    internal const int MaxConcurrentFeeds = 4;

    public async ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken)
    {
        var resolved = Resolve(scope);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var context = (resolved.Value.Settings, resolved.Value.Scrubbers);
        using var slots = new SemaphoreSlim(MaxConcurrentFeeds);
        var rows = await Task.WhenAll(resolved.Value.Feeds.Select(feed => TickOne(feed, context, slots, honorBackoff: true, cancellationToken))).ConfigureAwait(false);
        return new SyncReport(rows);
    }

    public async ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken)
    {
        var resolved = Resolve(scope);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var feed = resolved.Value.Feeds.FirstOrDefault(f => f.Source == source);
        if (feed is null)
        {
            var known = resolved.Value.Feeds.Select(f => f.Source.Value).ToList();
            return ContextErrors.SyncSourceUnknown.With($"'{source.Value}' isn't a record source this profile syncs ({(known.Count == 0 ? "none" : string.Join(", ", known))}); fix: sindri index status");
        }

        var context = (resolved.Value.Settings, resolved.Value.Scrubbers);
        using var slots = new SemaphoreSlim(1);
        return new SyncReport([await TickOne(feed, context, slots, honorBackoff: false, cancellationToken).ConfigureAwait(false)]);
    }

    public async ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken)
    {
        var resolved = Resolve(scope);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var rows = new List<SourceDecisions>();
        if (resolved.Value.Feeds.Count == 0)
        {
            return new DecisionReport(rows);
        }

        if (decisions is not null)
        {
            var context = (resolved.Value.Settings, resolved.Value.Scrubbers);
            var after = await decisions.ForTick(scope, context, resolved.Value.Repos, cancellationToken).ConfigureAwait(false);
            foreach (var feed in resolved.Value.Feeds)
            {
                if (after(feed) is not { } step)
                {
                    continue;
                }

                // why: no source lock; the pass reads the stored records and writes the intent layer, so a pull of the same source goes on beside it
                var row = await step(new SourceSync(feed.Source, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Code: null)).ConfigureAwait(false);
                rows.Add(new SourceDecisions(feed.Source, row.Linked + row.Unlinked, row.Decisions, row.Code));
            }
        }

        // why: after the per-record pass, which it leaves alone; it sends record text to a model, so it needs labeling on as well as its own switch
        var settings = resolved.Value.Settings;
        var synthesized = synthesis is null || !settings.ConceptSynthesis.Enabled || !settings.Labeling.Enabled
            ? null
            : await synthesis.Run(settings.ConceptSynthesis, settings.Labeling, settings.ExtraPatterns, cancellationToken).ConfigureAwait(false);
        return new DecisionReport(rows, synthesized);
    }

    private async Task<SourceSync> TickOne(RecordFeed feed, (ContextSettings Settings, Scrubbers Scrubbers) context, SemaphoreSlim slots, bool honorBackoff, CancellationToken cancellationToken)
    {
        // why: the scheduled tick leaves a failing source alone until its next try; a named sync is the owner asking for it now, so it never waits
        if (honorBackoff && await feeds.Backoff(feed.Source, cancellationToken).ConfigureAwait(false) is { Failures: > 0, NextTry: { } nextTry } && feeds.Now() < nextTry)
        {
            var last = await feeds.LastOutcome(feed.Source, cancellationToken).ConfigureAwait(false);
            return Failed(feed.Source, last?.Code ?? ContextErrors.SourceUnavailable.Code);
        }

        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var capped = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            capped.CancelAfter(FeedCap);
            var row = await feeds.Run(feed, (context.Scrubbers, context.Settings), steps?.For(feed, context.Settings, capped.Token), capped.Token).ConfigureAwait(false);
            return row.IsError ? Failed(feed.Source, row.Error.Code) : row.Value;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // why: its own cap fired; the pages committed so far stay with the stored cursor, so the next tick resumes
            await feeds.Note(feed.Source, ContextErrors.SyncCapped.Code, cancellationToken).ConfigureAwait(false);
            return Failed(feed.Source, ContextErrors.SyncCapped.Code);
        }
        finally
        {
            _ = slots.Release();
        }
    }

    private static SourceSync Failed(SourceId source, string code) => new(source, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, code);

    public async ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken)
    {
        var resolved = Resolve(scope);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var rows = new List<SourceProgress>();
        foreach (var feed in resolved.Value.Feeds)
        {
            var position = await store.Position(feed.Source, cancellationToken).ConfigureAwait(false);
            if (position.IsError)
            {
                return position.Error;
            }

            var row = await ProgressRow(feed.Source, position.Value.Phase, cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    private async ValueTask<Result<SourceProgress>> ProgressRow(SourceId source, SyncPhase? phase, CancellationToken cancellationToken)
    {
        var counted = await store.Count(source, cancellationToken).ConfigureAwait(false);
        if (counted.IsError)
        {
            return counted.Error;
        }

        // why: only the Linear issue feed is reset to Recent with rows already stored (migration 0020), so only it is checked for a re-read; any other source in Recent is on its first read
        int? stored = source == LinearSourceIds.Issues && phase is SyncPhase.Recent ? counted.Value.Records : null;
        var last = await feeds.LastOutcome(source, cancellationToken).ConfigureAwait(false);
        var backoff = await feeds.Backoff(source, cancellationToken).ConfigureAwait(false);
        return SourceProgress.From(source, phase, last?.At, last?.Code, stored) with
        {
            Pull = SourceBackoff.Pull(backoff, feeds.IsRunning(source), feeds.Now()),
            Records = counted.Value.Records,
            LastCommit = backoff.LastCommit,
            LastError = last?.Code is { } code && code != ContextErrors.SyncCapped.Code ? code : null,
            LastErrorText = backoff.LastErrorText,
            Failures = backoff.Failures,
            NextTry = backoff.NextTry,
        };
    }

    public async ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var enabled = settings.Value.ConceptSynthesis.Enabled && settings.Value.Labeling.Enabled;
        if (concepts is null)
        {
            return Core.ConceptSynthesis.ConceptStatus.From(enabled, [], watermark: 0, live: 0, embedded: 0);
        }

        // why: one count query each side, so a status read never pages the record store
        var stored = await concepts.All(cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var watermark = await concepts.Watermark(cancellationToken).ConfigureAwait(false);
        if (watermark.IsError)
        {
            return watermark.Error;
        }

        var live = await store.LiveCounts(cancellationToken).ConfigureAwait(false);
        return live.IsError
            ? live.Error
            : Core.ConceptSynthesis.ConceptStatus.From(enabled, stored.Value, watermark.Value, live.Value.Live, live.Value.Embedded);
    }

    public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => store.Count(source, cancellationToken);

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => feeds.Exclusive(source, () => store.Purge(source, cancellationToken));

    private Result<(ContextSettings Settings, Scrubbers Scrubbers, IReadOnlyList<ContextRepo> Repos, IReadOnlyList<RecordFeed> Feeds)> Resolve(ContextScope scope)
    {
        var settings = SettingsReader.Read(scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller));
        return repos.Bind(r => Results.Success((settings.Value, scrubbers.Value, r, feeds.For(settings.Value, r, scrubbers.Value))));
    }
}
