using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Linking;

/// <summary>The intent layer's store (spec §6.2, S-5B): what syncs wrote, outside the map versions, so a build or promote never drops it.</summary>
public interface IIntentStore
{
    /// <summary>Everything stored, as <see cref="IntentOverlay"/> merges it.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The layer (empty before any sync), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken);

    /// <summary>One source's sync state.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state (zeros before any sync), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken);

    /// <summary>In one transaction stores the page's records and links and moves the source's cursor; a failure changes nothing, cursor included. A replay of the same page writes no new rows.</summary>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What it did, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken);

    /// <summary>Records that a run of the source failed, keeping its cursor and last sync.</summary>
    /// <param name="source">The source.</param>
    /// <param name="code">The error code.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken);

    /// <summary>The records of a source whose latest version hasn't been extracted yet.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The queued record keys.</returns>
    public ValueTask<Result<IReadOnlyList<RecordKey>>> Queued(SourceId source, CancellationToken cancellationToken);

    /// <summary>The records of a source whose links were last computed against the given map version.</summary>
    /// <param name="source">The source.</param>
    /// <param name="mapVersion">The map version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record keys.</returns>
    public ValueTask<Result<IReadOnlyList<RecordKey>>> LinkedUnder(SourceId source, int mapVersion, CancellationToken cancellationToken);

    /// <summary>Replaces a record's decisions and their edges, and marks its version extracted (only if the record still has that version).</summary>
    /// <param name="batch">What extraction found.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> SaveDecisions(DecisionBatch batch, CancellationToken cancellationToken);

    /// <summary>Queues a record again without touching its decisions (only if the record still has that version): a done record whose re-extraction was held back, so the next sync tries it again.</summary>
    /// <param name="record">The record.</param>
    /// <param name="version">The record version that was held back.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> Requeue(RecordKey record, string version, CancellationToken cancellationToken);

    /// <summary>Replaces a record's aliases without touching its extraction state (only if the record still has that version): a same-version row never gets new aliases from <see cref="CommitPage"/>.</summary>
    /// <param name="record">The record.</param>
    /// <param name="version">The record version the aliases belong to.</param>
    /// <param name="aliases">The aliases.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> SetAliases(RecordKey record, string version, IReadOnlyList<string> aliases, CancellationToken cancellationToken);

    /// <summary>Replaces one rule's links of a record and nothing else: its other links, extraction state and <see cref="LinkedUnder"/> map version stay as they are (concept pins spec §3.4).</summary>
    /// <param name="record">The record.</param>
    /// <param name="rule">The rule whose links are replaced.</param>
    /// <param name="links">The new links of that rule (all from <paramref name="record"/>, all of <paramref name="rule"/>); empty removes them.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> ReplaceRuleLinks(RecordKey record, LinkRule rule, IReadOnlyList<IntentLink> links, CancellationToken cancellationToken);

    /// <summary>Stores a checked pair (a pair is stored once).</summary>
    /// <param name="conflict">The pair and its verdict.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> SaveConflict(DecisionConflict conflict, CancellationToken cancellationToken);
}
