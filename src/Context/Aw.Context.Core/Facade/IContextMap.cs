using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The map part of the facade (spec Phase 3, stage S-3a): import, export, status, the doctor checks and the purge; stage S-4 adds the build, the review, the promote and the scoping proposals.
/// IContextEngine extends it, and so does IScopeProposals (the scoping run's proposals and their withdrawal); Sindri calls these through IContextEngine only. Reads take the same ContextScope as the rest of the engine.
/// </summary>
public interface IContextMap : IScopeProposals
{
    /// <summary>Reads and checks a map file without writing anything: what 'context map import' shows before asking for the hash.</summary>
    /// <param name="path">The file.</param>
    /// <param name="scope">The profile's context section and repos: the file's repo must be one the profile maps.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The preview, or SND-CONTEXT-001/005/031/032.</returns>
    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Imports a map file as the current version (a protected command: Sindri checks the caller first).</summary>
    /// <param name="path">The file.</param>
    /// <param name="confirmedHash">The content hash the owner confirmed.</param>
    /// <param name="scope">The profile's context section and repos: the file's repo must be one the profile maps.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new version, or SND-CONTEXT-001/004/005/031/032/034 with nothing written.</returns>
    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Exports the current version (with the override layer) to the exports folder.</summary>
    /// <param name="path">A name or path under the exports folder, or null.</param>
    /// <param name="scope">The profile's context section and repos, and the caller: a session can't export (SND-CONTEXT-020).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The file, or SND-CONTEXT-001/003/005/020/030/033.</returns>
    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The current version and its counts.</summary>
    /// <param name="anchor">A node key, alias or label whose own scope the answer also reports the coverage of, or null for none.</param>
    /// <param name="scope">The profile's context section and repos, and the caller: a session sees its repo's scope only.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status, or SND-CONTEXT-001/003/020/030.</returns>
    public ValueTask<Result<MapStatusView>> MapStatus(string? anchor, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Doctor 'context:map': whether there is a current map and whether overrides are orphaned, counted over the map as the profile's deny globs leave it (as status counts it).</summary>
    /// <param name="scope">The approved profile's context section and repos with a service caller, or null when no profile is approved (the stored map is counted).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The check.</returns>
    public ValueTask<Result<MapHealth>> CheckMap(ContextScope? scope, CancellationToken cancellationToken);

    /// <summary>Doctor 'context:retention': expired exports and retained versions the purge should have removed.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The check.</returns>
    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken);

    /// <summary>The 'context-purge' service's work: deletes expired exports and retained versions beyond the newest 100.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was removed.</returns>
    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken);

    /// <summary>
    /// Proposes the map from the repo (spec Phase 4): deterministic anchors with file-level 'implements', graphify clusters as
    /// candidate features, an optional labeling pass; writes a draft and re-applies every override. Takes the heavy-job lock.
    /// </summary>
    /// <param name="progress">Told one line per step ('extracting n/N files'), or null.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the build; nothing becomes a draft then.</param>
    /// <returns>The report, or SND-CONTEXT-001/004/005/040/042/048.</returns>
    public ValueTask<Result<MapBuildReport>> BuildMap(IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>What is left to decide in the latest draft.</summary>
    /// <param name="limit">The most entries per group (1 to 10000), or null for 100.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The queue, or SND-CONTEXT-001/004/044.</returns>
    public ValueTask<Result<ReviewQueueView>> ReviewQueue(int? limit, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Applies one review decision: one override row (accept-deterministic: one per entry). Sindri checks the verb's class first (X63).</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows written, or SND-CONTEXT-001/004/044/047 (stale target) with nothing written.</returns>
    public ValueTask<Result<ReviewResult>> ApplyReview(ReviewDecision decision, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The override log, newest first, with what each undo cancelled: the ids "map review --undo" takes.</summary>
    /// <param name="limit">The most entries (1 to 10000), or null for 200.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The history, or SND-CONTEXT-001/004/020.</returns>
    public ValueTask<Result<ReviewHistoryView>> ReviewHistory(int? limit, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Writes the queue as a review file under the exports folder, to be edited and applied with <see cref="ApplyReviewFile"/>.</summary>
    /// <param name="path">A name or path under the exports folder, or null.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The file, or SND-CONTEXT-001/004/005/033/044.</returns>
    public ValueTask<Result<ReviewExported>> ExportReview(string? path, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Checks a review file and answers its hash and counts, writing nothing: what the protected apply shows before the human's typed confirmation.</summary>
    /// <param name="path">The file.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The preview, or SND-CONTEXT-001/005/020/031.</returns>
    public ValueTask<Result<ReviewFilePreview>> PreviewReviewFile(string path, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Applies a review file (a protected command: Sindri checks the caller first). Entries that match nothing in the latest draft are listed as skipped.</summary>
    /// <param name="path">The file.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows written and the entries skipped, or SND-CONTEXT-001/004/005/043/044.</returns>
    public ValueTask<Result<ReviewResult>> ApplyReviewFile(string path, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// The cleanup agent (owner F1, 2026-10-19): settles every entry of the latest draft's review queue so the draft can be promoted with no human step.
    /// Deterministic entries are accepted, orphaned overrides dropped, model entries at or above <see cref="CleanupPolicy.KeepAt"/> accepted, and the ones
    /// below it judged by a model (keep: accept, remove: reject); when no model answers they are rejected. Every row is written as the caller. A session is refused.
    /// </summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the pass; the rows already written stay (each is an ordinary review decision).</param>
    /// <param name="expectedVersion">The draft the caller means (the automatic publish passes the one it decided about), or null for whichever is latest (the human path). The pass checks it on every read, and its rows are appended only while that draft is still the latest.</param>
    /// <returns>What it decided, or SND-CONTEXT-001/004/020/044/047/190 (190: <paramref name="expectedVersion"/> is given and the latest draft is another number, or there is none).</returns>
    public ValueTask<Result<CleanupReport>> CleanDraft(ContextScope scope, CancellationToken cancellationToken, int? expectedVersion = null);

    /// <summary>Makes the latest draft current (a protected command with the typed challenge: Sindri checks the caller first).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="expectedVersion">The draft the caller means, or null for whichever is latest (the human path, which is never held). Bound to the draft read at the start of the call.</param>
    /// <returns>The new current version, or SND-CONTEXT-001/004/046 (no draft, or undecided items) or 190 (<paramref name="expectedVersion"/> is given and the latest draft is another number, or there is none). Every promote clears the hold and the wait.</returns>
    public ValueTask<Result<MapPromoted>> Promote(ContextScope scope, CancellationToken cancellationToken, int? expectedVersion = null);

    /// <summary>What the publish rules read: the hold, the latest draft, the current version and its heads (service use).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller: a session is refused.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state (all repos), or SND-CONTEXT-001/004/020.</returns>
    public ValueTask<Result<MapPublishState>> Held(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Holds the latest draft for the code index: stores the hold next to it, with the start of the wait read from the chain row in the same transaction (service use).</summary>
    /// <param name="hold">The draft to hold, its heads (checked, 7 to 40 lowercase hex) and the time now as <see cref="MapHold.Since"/>.</param>
    /// <param name="scope">The profile's context section and repos, and the caller: a session is refused.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stored hold (its <see cref="MapHold.Since"/> is the wait's start), or SND-CONTEXT-001/004/020/190 (the draft is not the latest)/191 (a head is invalid).</returns>
    public ValueTask<Result<MapHold>> Hold(MapHold hold, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Drops the hold on a draft; the wait's start (the chain) stays, and so does the draft (service use).</summary>
    /// <param name="version">The held draft.</param>
    /// <param name="scope">The profile's context section and repos, and the caller: a session is refused.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit (also when that draft is not held), or SND-CONTEXT-001/004/020.</returns>
    public ValueTask<Result<Unit>> Release(int version, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The publish state for everyone else: 'context map status', the Health card and a session. Only the caller's repos appear.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller (a session sees its own repo, while sessions are on).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, or SND-CONTEXT-001/003/004/020.</returns>
    public ValueTask<Result<MapPublishView>> PublishStatus(ContextScope scope, CancellationToken cancellationToken);
}
