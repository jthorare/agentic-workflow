using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Core.Lenses;

/// <summary>The rows of <see cref="LensSearched.Sources"/>.</summary>
public static class SearchedSources
{
    /// <summary>The row of an intent source: not-configured when the profile doesn't name it, degraded with the last run's code when it failed, unavailable ('not-synced') before the first sync, else ok at its last sync.</summary>
    /// <param name="id">The source ('onyx').</param>
    /// <param name="configured">Whether the profile names it.</param>
    /// <param name="state">What its sync stored, or null when it has no state yet.</param>
    /// <returns>The row.</returns>
    public static SearchedSource Intent(SourceId id, bool configured, IntentSourceState? state)
    {
        if (!configured)
        {
            return new SearchedSource(id, SearchedState.NotConfigured, NotConfigured, Code: null, AsOf: null);
        }

        if (state is null || (state.LastSync is null && state.LastError is null))
        {
            return new SearchedSource(id, SearchedState.Unavailable, NotSynced, ContextErrors.IntentNotSynced.Code, AsOf: null);
        }

        // A capped run (059) or held-back extraction (042) is partial, not a failure: the source still answers.
        var failed = state.LastError is { } code && code != ContextErrors.SyncCapped.Code && code != ContextErrors.LabelingUnavailable.Code;
        return new SearchedSource(id, failed ? SearchedState.Degraded : SearchedState.Ok, Reason: null, state.LastError, state.LastSync);
    }

    /// <summary>The row of an owned index source: degraded with the last tick's code when it failed, unavailable ('not-synced') before it has data, else ok at its last sync.</summary>
    /// <param name="progress">The source's sync progress.</param>
    /// <returns>The row.</returns>
    public static SearchedSource Index(SourceProgress progress)
    {
        // A capped run (059) is partial, not a failure: the source still answers.
        if (progress.LastCode is { } code && code != ContextErrors.SyncCapped.Code)
        {
            return new SearchedSource(progress.Source, SearchedState.Degraded, Reason: null, code, progress.LastSync);
        }

        if (progress.Phase is null || !progress.Usable)
        {
            return new SearchedSource(progress.Source, SearchedState.Unavailable, NotSynced, ContextErrors.IntentNotSynced.Code, AsOf: null);
        }

        return new SearchedSource(progress.Source, SearchedState.Ok, Reason: null, Code: null, progress.LastSync);
    }

    /// <summary>The map's row: ok, as of the version's creation.</summary>
    internal static SearchedSource Map(DateTimeOffset created) => new(SourceId.From("map"), SearchedState.Ok, Reason: null, Code: null, created);

    /// <summary>The code index's row: not built until the code stage ships it.</summary>
    internal static SearchedSource CodeIndex { get; } = new(SourceId.From("code-index"), SearchedState.Unavailable, "index-not-built", Code: null, AsOf: null);

    /// <summary>The history lens's reading of a synced intent source that linked no record to any node: unavailable, 'history-unlinked'.</summary>
    /// <param name="row">The source's row.</param>
    /// <returns>The row, changed only when it was ok.</returns>
    public static SearchedSource Unlinked(SearchedSource row) =>
        row.State == SearchedState.Ok ? row with { State = SearchedState.Unavailable, Reason = "history-unlinked" } : row;

    /// <summary>The git-history row of a history answer.</summary>
    /// <param name="state">The state.</param>
    /// <param name="reason">The recorded-limit word, or null.</param>
    /// <param name="code">The code, or null.</param>
    /// <param name="asOf">When the log was read.</param>
    /// <returns>The row.</returns>
    public static SearchedSource GitHistory(SearchedState state, string? reason, string? code, DateTimeOffset asOf) => new(SourceId.From("git-history"), state, reason, code, asOf);

    /// <summary>The incidents row: no incident source exists yet.</summary>
    public static SearchedSource Incidents { get; } = new(SourceId.From("incidents"), SearchedState.NotConfigured, NotConfigured, Code: null, AsOf: null);

    private const string NotSynced = "not-synced";
    /// <summary>The row of a lexical code source ('lexical-tests', 'lexical-schema'): what the code lens read from a repo's files at a commit (S-CL).</summary>
    /// <param name="id">'lexical-tests' or 'lexical-schema'.</param>
    /// <param name="state">The state.</param>
    /// <param name="reason">A recorded-limit word ('not-configured', 'commit-not-held', 'capped'), or null.</param>
    /// <param name="code">An SND-CONTEXT-* code, or null.</param>
    /// <param name="asOf">When it was read, or null.</param>
    /// <returns>The row.</returns>
    public static SearchedSource Lexical(SourceId id, SearchedState state, string? reason, string? code, DateTimeOffset? asOf) =>
        new(id, state, reason, code, asOf);

    private const string NotConfigured = "not-configured";
}
