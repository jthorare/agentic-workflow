using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The context engine's one facade (spec §4): every surface (HTTP, CLI, MCP, scheduled services, doctor) calls it with
/// Core records only. Sindri passes the approved profile's 'context:' section, its repos and the caller on every call;
/// the engine keeps no profile of its own.
/// </summary>
public interface IContextEngine : IContextMap, IContextGraph, IContextLenses, IContextCode, IContextHistory
{
    /// <summary>Validates a profile's 'context:' section against the profile's repos and applies its defaults.</summary>
    /// <param name="section">The section, or null when the profile has none.</param>
    /// <param name="repos">The profile's repos as written: with a section, 'context.repos' may name only these, and every repo in scope needs a name the engine can use.</param>
    /// <returns>The settings, or SND-CONTEXT-001 naming the key path ('context.deadline: …', 'context.repos: …').</returns>
    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos);

    /// <summary>Answers one question (spec §8): anchors, candidates, fusion, stale check, packing, citations.</summary>
    /// <param name="query">The question.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The cited result (possibly degraded or empty), or a catalogue error (§8.6).</returns>
    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Each configured source's state (spec §8.5 'context sources status').</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sources view.</returns>
    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a configured intent source has never synced successfully (never run, or only failed runs), read from the stored sync state: no live source is probed, so a scheduler may ask every minute.
    /// </summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a configured intent source has no successful sync; false when none is configured or all have synced.</returns>
    public ValueTask<Result<bool>> HasSyncGap(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Each mapped repo's checkout HEAD and whether it is on the default branch, read from local refs (no fetch): what an automatic refresh compares with the heads it last mapped.</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One head per mapped repo ('context.repos' within the profile's repos), or SND-CONTEXT-001/048.</returns>
    public ValueTask<Result<IReadOnlyList<RepoHead>>> MapHeads(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Runs a probe file (spec §9.2 item 3): reads and checks it, asks each question through <paramref name="ask"/> (Sindri's
    /// CLI passes its GET /sindri/context/query, so probes see what callers see), scores the answers, compares them with the
    /// baseline of the same file (its newest complete run that did not regress) and writes the report under $AW_SINDRI_HOME/context/eval/probes/.
    /// </summary>
    /// <param name="file">The probe file's absolute path, or null for $AW_SINDRI_HOME/context/eval/probes.json.</param>
    /// <param name="ask">Answers one probe's question.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The run (its report says whether it regressed, SND-CONTEXT-038, or is incomplete because a probe's query failed, every probe included), or SND-CONTEXT-004/005/035/036.</returns>
    public ValueTask<Result<ProbeRun>> EvalProbes(string? file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, CancellationToken cancellationToken);

    /// <summary>Brings the snapshot sources (repo-docs' index) up to date with each repo's HEAD.</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    /// <returns>What changed per repo.</returns>
    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Syncs the intent sources (and refreshes the repo docs): walks the current map's nodes, searches each intent source for them, links what it finds by rule and, with labeling on, extracts decisions (spec §8.8, S-5B).
    /// A source that is down is a row with its code, not an error.
    /// </summary>
    /// <param name="source">One source's id, or null for every source.</param>
    /// <param name="scope">The caller's scope.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>One row per source, or SND-CONTEXT-030 (no map), 056 (already running), 058 (unknown source).</returns>
    public ValueTask<Result<SyncReport>> Sync(string? source, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Runs the backtest: the eligible samples, the as-of maps, the arms, the pass line (spec §9.1, S-7A). A human or service job; a session is refused (SND-CONTEXT-020).</summary>
    /// <param name="request">The scope, arms and window.</param>
    /// <param name="cancellationToken">Cancels the run; a cancelled run writes no report.</param>
    /// <returns>The report and its path, SND-CONTEXT-080 with no eligible sample, 081 with no brief source, 040 with the heavy lock busy, or another context error.</returns>
    public ValueTask<Result<BacktestRun>> EvalBacktest(BacktestRequest request, CancellationToken cancellationToken);

    /// <summary>Measures the current map against the owner's decisions (spec §9.2 item 2). A human or service call.</summary>
    /// <param name="request">The scope.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The report and its path, SND-CONTEXT-030 with no map, or SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<MapQualityRun>> EvalMap(MapQualityRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes eval reports and cache files past 90 days, or all with <see cref="EvalPurgeRequest.All"/> (probe baselines too).</summary>
    /// <param name="request">The scope and the mode.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was removed.</returns>
    public ValueTask<Result<EvalPurgeReport>> EvalPurge(EvalPurgeRequest request, CancellationToken cancellationToken);
}
