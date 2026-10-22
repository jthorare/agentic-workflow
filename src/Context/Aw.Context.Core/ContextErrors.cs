using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>
/// The context engine's error catalogue (spec §8.6), codes SND-CONTEXT-NNN. Stage S-1A owns 001–019 and defines 020
/// (the session gate, spec §3); S-3a adds 030–034 (the map file and its store); S-3b adds 035–038 (the probe runner and anchors; 039 is reserved); S-4 adds 040 and 042–048 (041 stays retired, 049 is reserved); S-7A adds 080–083 (backtest; 084–086 reserved for review findings); S-5A adds 050–055 (Onyx, in the
/// S-5A/B range 050–069); later stages
/// add theirs in their ranges. HTTP status follows the kind; the CLI exit code is 1 for attention (<see cref="Attention"/>) and 2 for a tool error.
/// </summary>
public static class ContextErrors
{
    /// <summary>SND-CONTEXT-001: the profile's 'context:' section is invalid; the message names the key path.</summary>
    public static readonly ErrorDefinition SettingsInvalid = new("SND-CONTEXT-001", ErrorKind.Invalid, "The profile's context section is invalid");

    /// <summary>SND-CONTEXT-002: a source the profile names isn't configured, or its secret pointer doesn't resolve.</summary>
    public static readonly ErrorDefinition SourceNotConfigured = new("SND-CONTEXT-002", ErrorKind.Invalid, "A context source isn't configured");

    /// <summary>SND-CONTEXT-003: an unknown anchor, or a record outside a session's repo scope (answered the same way).</summary>
    public static readonly ErrorDefinition UnknownAnchor = new("SND-CONTEXT-003", ErrorKind.NotFound, "Unknown anchor");

    /// <summary>SND-CONTEXT-004: the store can't open, or its schema doesn't match this build.</summary>
    public static readonly ErrorDefinition StoreUnavailable = new("SND-CONTEXT-004", ErrorKind.Unavailable, "The context store can't be opened");

    /// <summary>SND-CONTEXT-005: a map file or an output path inside a git worktree.</summary>
    public static readonly ErrorDefinition PathInWorktree = new("SND-CONTEXT-005", ErrorKind.Invalid, "Output path inside a git worktree");

    /// <summary>SND-CONTEXT-006: the question itself is invalid (empty, two shapes, a value out of range).</summary>
    public static readonly ErrorDefinition QueryInvalid = new("SND-CONTEXT-006", ErrorKind.Invalid, "The context query is invalid");

    /// <summary>SND-CONTEXT-010: one source is unavailable; the query goes on without it (degraded).</summary>
    public static readonly ErrorDefinition SourceUnavailable = new("SND-CONTEXT-010", ErrorKind.Unavailable, "A context source is unavailable");

    /// <summary>SND-CONTEXT-011: every source is unavailable (a tool error, not "no results").</summary>
    public static readonly ErrorDefinition AllSourcesUnavailable = new("SND-CONTEXT-011", ErrorKind.Unavailable, "Every context source is unavailable");

    /// <summary>SND-CONTEXT-020: session reads are off ('context.sessions.enabled: false'), on every transport; also a session asking for a human or service job (map import, preview, build, export, review).</summary>
    public static readonly ErrorDefinition SessionsDisabled = new("SND-CONTEXT-020", ErrorKind.Forbidden, "Context reads and the map export from sessions are off");

    /// <summary>SND-CONTEXT-021: an MCP call without X-Sindri-Repo, so it has no repo scope (stage S-1M).</summary>
    public static readonly ErrorDefinition McpRepoMissing = new("SND-CONTEXT-021", ErrorKind.Invalid, "The MCP call names no repo");

    /// <summary>SND-CONTEXT-022: an MCP tool's arguments don't match its input schema (S-1M).</summary>
    public static readonly ErrorDefinition McpArgumentsInvalid = new("SND-CONTEXT-022", ErrorKind.Invalid, "The MCP tool's arguments are invalid");

    /// <summary>SND-CONTEXT-023: an MCP tool Sindri doesn't have (S-1M).</summary>
    public static readonly ErrorDefinition McpToolUnknown = new("SND-CONTEXT-023", ErrorKind.NotFound, "Unknown MCP tool");

    /// <summary>SND-CONTEXT-024: 'sindri mcp register' outside every profile repo, or with a repo the profile doesn't name (S-1M).</summary>
    public static readonly ErrorDefinition McpRegisterRepoUnknown = new("SND-CONTEXT-024", ErrorKind.Invalid, "Not a repo the profile names");

    /// <summary>SND-CONTEXT-025: 'sindri mcp register --apply' couldn't run the claude CLI, or it failed (S-1M).</summary>
    public static readonly ErrorDefinition McpRegisterFailed = new("SND-CONTEXT-025", ErrorKind.Unavailable, "The claude CLI couldn't register the MCP server");

    /// <summary>SND-CONTEXT-026: a user-wide 'sindri' MCP entry in a provider's config (doctor context:mcp-scope; S-1M).</summary>
    public static readonly ErrorDefinition McpUserWide = new("SND-CONTEXT-026", ErrorKind.Conflict, "A user-wide sindri MCP entry");

    /// <summary>SND-CONTEXT-030: no map yet (fix: sindri context map import &lt;file&gt;).</summary>
    public static readonly ErrorDefinition NoMap = new("SND-CONTEXT-030", ErrorKind.NotFound, "No map yet");

    /// <summary>SND-CONTEXT-031: the map file is invalid (schema version, shape, an unknown kind or a disallowed edge); the message names the key path and nothing was written.</summary>
    public static readonly ErrorDefinition MapFileInvalid = new("SND-CONTEXT-031", ErrorKind.Invalid, "The map file is invalid");

    /// <summary>SND-CONTEXT-032: the map file refers to a node it doesn't define (an edge end, an alias target, a decided node); nothing was written.</summary>
    public static readonly ErrorDefinition MapNodeUndefined = new("SND-CONTEXT-032", ErrorKind.Invalid, "The map file refers to an undefined node");

    /// <summary>SND-CONTEXT-033: an export path outside $AW_SINDRI_HOME/context/exports/ (a worktree path is SND-CONTEXT-005).</summary>
    public static readonly ErrorDefinition ExportOutsideHome = new("SND-CONTEXT-033", ErrorKind.Invalid, "Export path outside the exports folder");

    /// <summary>SND-CONTEXT-034: the map file changed since its content hash was confirmed; nothing was written.</summary>
    public static readonly ErrorDefinition MapFileChanged = new("SND-CONTEXT-034", ErrorKind.Conflict, "The map file changed since its hash was confirmed");

    /// <summary>SND-CONTEXT-035: the probe file is invalid (its shape or schema version, an unknown key, a duplicate, empty or non-slug id, an empty, wordless or over-long question); the message names the key path and nothing was run.</summary>
    public static readonly ErrorDefinition ProbeFileInvalid = new("SND-CONTEXT-035", ErrorKind.Invalid, "The probe file is invalid");

    /// <summary>SND-CONTEXT-036: no probe file: the one named doesn't exist, or none was named and $AW_SINDRI_HOME/context/eval/probes.json doesn't exist.</summary>
    public static readonly ErrorDefinition NoProbeFile = new("SND-CONTEXT-036", ErrorKind.NotFound, "No probe file");

    /// <summary>SND-CONTEXT-037: a node name (label) matches several nodes; the message lists their keys.</summary>
    public static readonly ErrorDefinition AmbiguousAnchor = new("SND-CONTEXT-037", ErrorKind.Conflict, "The name matches several nodes");

    /// <summary>SND-CONTEXT-038: a probe run scored lower than the baseline of the same probe file (its newest complete run that did not regress) in precision@5 or citation correctness, or a probe returned more absent nodes than it did there.</summary>
    public static readonly ErrorDefinition ProbesRegressed = new("SND-CONTEXT-038", ErrorKind.Conflict, "The probes regressed");

    /// <summary>SND-CONTEXT-040: the heavy-job lock stayed busy; the message names the holder (CLI exit 75, HTTP 503).</summary>
    public static readonly ErrorDefinition HeavyLockBusy = new("SND-CONTEXT-040", ErrorKind.Unavailable, "The heavy-job lock stayed busy");

    /// <summary>SND-CONTEXT-042: labeling is off ('context.labeling'), or the pass would go over its token cap.</summary>
    public static readonly ErrorDefinition LabelingUnavailable = new("SND-CONTEXT-042", ErrorKind.Invalid, "Labeling is off or over its token cap");

    /// <summary>SND-CONTEXT-043: no entry of the review file matches the latest draft (the others are listed as skipped).</summary>
    public static readonly ErrorDefinition ReviewFileNoMatch = new("SND-CONTEXT-043", ErrorKind.Conflict, "No entry in the review file matches the latest draft");

    /// <summary>SND-CONTEXT-044: there is no draft map: 'draft' was asked on lenses or filemap, or the review queue or a review decision that needs a draft was asked with none.</summary>
    public static readonly ErrorDefinition NoDraftMap = new("SND-CONTEXT-044", ErrorKind.NotFound, "No draft map");

    /// <summary>SND-CONTEXT-045: a repo the profile doesn't name, or one outside a session's X-Sindri-Repo (answered the same way, so the scope isn't revealed).</summary>
    public static readonly ErrorDefinition RepoOutsideScope = new("SND-CONTEXT-045", ErrorKind.NotFound, "Repo outside the scope");

    /// <summary>SND-CONTEXT-046: 'map promote' refused: there is no draft, the draft still has undecided review items (the ones the review queue shows, not those a deny glob hides), or the draft changed after you confirmed.</summary>
    public static readonly ErrorDefinition PromoteRefused = new("SND-CONTEXT-046", ErrorKind.Conflict, "The draft can't be promoted");

    /// <summary>SND-CONTEXT-047: a stale review target: the node or link isn't in the latest draft's queue (decided already, or gone), or an undo names an override that is unknown or already undone.</summary>
    public static readonly ErrorDefinition ReviewTargetStale = new("SND-CONTEXT-047", ErrorKind.Conflict, "The review target is stale");

    /// <summary>SND-CONTEXT-048: extraction couldn't read the repo from git at the commit (a missing repo or sha).</summary>
    public static readonly ErrorDefinition ExtractionUnreadable = new("SND-CONTEXT-048", ErrorKind.Unavailable, "Extraction can't read the repo from git");

    /// <summary>
    /// SND-CONTEXT-050: Onyx refused the key (HTTP 403): it is missing, wrong, or lacks the admin role that
    /// 'POST /api/admin/search' needs (spec §8.8). Fix: use a personal access token of a dedicated admin user (or a service-account key),
    /// store it behind 'context.sources.onyx.token', and approve the profile again.
    /// </summary>
    public static readonly ErrorDefinition OnyxKeyRefused = new("SND-CONTEXT-050", ErrorKind.Forbidden, "Onyx refused the key: it is missing, wrong, or lacks the admin role the search route needs");

    /// <summary>
    /// SND-CONTEXT-051: Onyx answered with an unexpected status or shape (a route or field the pinned v4.9.0 doesn't have).
    /// The query goes on without Onyx (Unavailable). Fix: check the running release against 'context.sources.onyx.version'.
    /// </summary>
    public static readonly ErrorDefinition OnyxUnexpectedReply = new("SND-CONTEXT-051", ErrorKind.Unavailable, "Onyx answered with an unexpected shape; check the pinned version");

    /// <summary>SND-CONTEXT-052: Onyx has no document with that id (it was deleted or its connector no longer indexes it).</summary>
    public static readonly ErrorDefinition OnyxDocumentNotFound = new("SND-CONTEXT-052", ErrorKind.NotFound, "Onyx has no such document");

    /// <summary>
    /// SND-CONTEXT-053: the Onyx token is an admin token of a person's account, not of Sindri's dedicated user
    /// (OnyxSettings.DedicatedAccountEmail, the user `sindri onyx up` makes, a STANDARD account) or a service account (spec
    /// §8.8). Doctor shows it as degraded. Fix: use a token of a dedicated admin user for Sindri alone.
    /// </summary>
    public static readonly ErrorDefinition OnyxKeyNotServiceAccount = new("SND-CONTEXT-053", ErrorKind.Forbidden, "The Onyx key doesn't belong to Sindri's dedicated account");

    /// <summary>
    /// SND-CONTEXT-055: Onyx isn't on a loopback address, or its version differs from the pinned one (spec §8.8). The
    /// message names the key path ('context.sources.onyx.url' or '.version') and the fix, never the token.
    /// </summary>
    public static readonly ErrorDefinition OnyxNotPinnedOrLoopback = new("SND-CONTEXT-055", ErrorKind.Invalid, "Onyx isn't on a loopback address, or its version differs from the pinned one");

    /// <summary>SND-CONTEXT-056: a sync of that source is already running (one at a time per source; CLI exit 1, HTTP 409).</summary>
    public static readonly ErrorDefinition SyncRunning = new("SND-CONTEXT-056", ErrorKind.Conflict, "A sync of that source is already running");

    /// <summary>SND-CONTEXT-057: a model answer the engine refused (a quote that isn't verbatim, a target outside the record's linked nodes, a malformed shape). Counted in a sync's 'refused', never failing it.</summary>
    public static readonly ErrorDefinition ModelAnswerRefused = new("SND-CONTEXT-057", ErrorKind.Invalid, "A model answer was refused");

    /// <summary>SND-CONTEXT-058: 'sources sync X' (X, the sync request's source) names a source that isn't configured or has nothing to sync (CLI exit 1, HTTP 404).</summary>
    public static readonly ErrorDefinition SyncSourceUnknown = new("SND-CONTEXT-058", ErrorKind.NotFound, "No such source to sync");

    /// <summary>SND-CONTEXT-059: a sync stopped at its time cap: the run is partial and resumes from its cursor next time (degraded, exit 0).</summary>
    public static readonly ErrorDefinition SyncCapped = new("SND-CONTEXT-059", ErrorKind.Unavailable, "A sync stopped at its time cap");

    /// <summary>SND-CONTEXT-080: no eligible backtest sample (no merge carries an issue key of a profile team in the window, or every candidate was left out; the message gives the counts).</summary>
    public static readonly ErrorDefinition NoEligibleSample = new("SND-CONTEXT-080", ErrorKind.NotFound, "No eligible backtest sample");

    /// <summary>SND-CONTEXT-081: no configured intent source can fetch issue records for a brief ('onyx' today; 'linear' after S-6).</summary>
    public static readonly ErrorDefinition NoBriefSource = new("SND-CONTEXT-081", ErrorKind.Unavailable, "No source can supply backtest briefs");

    /// <summary>SND-CONTEXT-082: '--arms' names an unknown arm; the message lists the arms and which are not available yet.</summary>
    public static readonly ErrorDefinition UnknownArm = new("SND-CONTEXT-082", ErrorKind.Invalid, "Unknown backtest arm");

    /// <summary>SND-CONTEXT-083: adjudication stopped at its 1.5M-token cap; the rest are unjudged (degraded, exit 0; never returned as a failure).</summary>
    public static readonly ErrorDefinition AdjudicationCapped = new("SND-CONTEXT-083", ErrorKind.Unavailable, "Adjudication stopped at its token cap");
    /// <summary>SND-CONTEXT-110: a sha of a diff range isn't a commit in the repo's clone (often a head from another clone or fork that isn't fetched yet).</summary>
    public static readonly ErrorDefinition CommitUnknown = new("SND-CONTEXT-110", ErrorKind.NotFound, "The repo's clone doesn't hold that commit");

    /// <summary>SND-CONTEXT-111: the two commits of a diff range share no merge base (unrelated histories).</summary>
    public static readonly ErrorDefinition NoMergeBase = new("SND-CONTEXT-111", ErrorKind.Invalid, "The two commits share no merge base");

    /// <summary>SND-CONTEXT-140: 'hops' names a hop that does not exist. Scoping stage S-CH owns 140–149 (141 is reserved; a profile range error is SettingsInvalid, 001).</summary>
    public static readonly ErrorDefinition HopsInvalid = new("SND-CONTEXT-140", ErrorKind.Invalid, "hops must be a comma list of state, flow, dependency and component");

    /// <summary>SND-CONTEXT-120: the map version asked for ('mapVersion=' / '--map-version') isn't held: never written, purged, a draft, still building, or its commit part differs. Scoping stage S-LB1 owns 120–129.</summary>
    public static readonly ErrorDefinition MapVersionNotRetained = new("SND-CONTEXT-120", ErrorKind.NotFound, "That map version is not retained");

    /// <summary>SND-CONTEXT-160: the history lens ran out of 'context.deadline' reading git and returned the changes it had. Never a route failure: it is the code of a 'git-history' row in 'searched' (state degraded). S-H owns 160–169.</summary>
    public static readonly ErrorDefinition HistoryDeadline = new("SND-CONTEXT-160", ErrorKind.Timeout, "The history read ran out of time");

    /// <summary>SND-CONTEXT-161: a configured intent source has never synced, so no record of it can be listed. The code of an intent source's row in 'searched' (state unavailable, reason 'not-synced'); never a route failure. Sindri's 'context-sync-gaps' service starts the sync.</summary>
    public static readonly ErrorDefinition IntentNotSynced = new("SND-CONTEXT-161", ErrorKind.Unavailable, "A configured intent source has not synced yet");

    /// <summary>SND-CONTEXT-130: a profile recognizer ('context.extract.recognizers[i]') is invalid: an unknown kind, a bad glob, a regex that doesn't compile or a key group it doesn't name. 132 is reserved for a recognizer that timed out on a file (degraded, never a failure).</summary>
    public static readonly ErrorDefinition RecognizerInvalid = new("SND-CONTEXT-130", ErrorKind.Invalid, "A profile recognizer is invalid");

    /// <summary>SND-CONTEXT-131: more than 50 profile recognizers, or a pattern over 512 characters.</summary>
    public static readonly ErrorDefinition RecognizerOverCap = new("SND-CONTEXT-131", ErrorKind.Invalid, "Too many profile recognizers, or a pattern is too long");

    /// <summary>SND-CONTEXT-150: a 'context.code.schemaGlobs' or 'context.code.testGlobs' entry isn't a repo-relative glob. The message starts with the key path.</summary>
    public static readonly ErrorDefinition CodeGlobInvalid = new("SND-CONTEXT-150", ErrorKind.Invalid, "A code-lens glob is not a repo-relative glob");

    /// <summary>SND-CONTEXT-151: more than 50 entries in 'context.code.schemaGlobs' or 'context.code.testGlobs'.</summary>
    public static readonly ErrorDefinition CodeGlobsTooMany = new("SND-CONTEXT-151", ErrorKind.Invalid, "Too many code-lens globs");

    /// <summary>SND-CONTEXT-152: the code lens's lexical search ran out of 'context.code.deadline' and returned what it had (the code of a searched row, never a route failure).</summary>
    public static readonly ErrorDefinition CodeSearchTimedOut = new("SND-CONTEXT-152", ErrorKind.Timeout, "The lexical code search ran out of time");

    /// <summary>SND-CONTEXT-133: a 'context.extract.graphify' limit (timeoutMinutes, maxSnapshotMB, maxFileMB, maxGraphMB) isn't a whole number from 0 (no limit) up. The message names the key path.</summary>
    public static readonly ErrorDefinition GraphifyLimitInvalid = new("SND-CONTEXT-133", ErrorKind.Invalid, "A graphify limit is invalid");

    /// <summary>SND-CONTEXT-090: the query's rerank (Phase 8, 'context.ranker.rerank') could not order the items: the model was unavailable, over budget, or answered no order. The query went on in its feature or fusion order; the answer lists 'rerank' as degraded (never a route failure).</summary>
    public static readonly ErrorDefinition RerankUnavailable = new("SND-CONTEXT-090", ErrorKind.Unavailable, "The rerank could not order the items");

    /// <summary>SND-CONTEXT-190: a promote, a cleanup, an override append or a hold named a draft ('expectedVersion') and the latest draft is another number or there is none: a rebuild replaced it, or it was promoted. The map's publish rules own 190-194 (retrieval spec §8.7a).</summary>
    public static readonly ErrorDefinition DraftChanged = new("SND-CONTEXT-190", ErrorKind.Conflict, "The draft is not the one that was expected");

    /// <summary>SND-CONTEXT-191: a map head names a commit that is not 7 to 40 lowercase hex characters, or a repo twice (a build report, a hold or an import).</summary>
    public static readonly ErrorDefinition MapHeadInvalid = new("SND-CONTEXT-191", ErrorKind.Invalid, "A map head is invalid");

    /// <summary>Every Context error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [SettingsInvalid, SourceNotConfigured, UnknownAnchor, StoreUnavailable, PathInWorktree, QueryInvalid, SourceUnavailable, AllSourcesUnavailable, SessionsDisabled,
         McpRepoMissing, McpArgumentsInvalid, McpToolUnknown, McpRegisterRepoUnknown, McpRegisterFailed, McpUserWide,
         NoMap, MapFileInvalid, MapNodeUndefined, ExportOutsideHome, MapFileChanged, ProbeFileInvalid, NoProbeFile, AmbiguousAnchor, ProbesRegressed,
         HeavyLockBusy, LabelingUnavailable, ReviewFileNoMatch, NoDraftMap, RepoOutsideScope, PromoteRefused, ReviewTargetStale, ExtractionUnreadable,
         OnyxKeyRefused, OnyxUnexpectedReply, OnyxDocumentNotFound, OnyxKeyNotServiceAccount, OnyxNotPinnedOrLoopback, SyncRunning, ModelAnswerRefused, SyncSourceUnknown, SyncCapped,
         NoEligibleSample, NoBriefSource, UnknownArm, AdjudicationCapped, CommitUnknown, NoMergeBase, MapVersionNotRetained, HistoryDeadline, IntentNotSynced, CodeGlobInvalid, CodeGlobsTooMany, CodeSearchTimedOut, RecognizerInvalid, RecognizerOverCap, DraftChanged, MapHeadInvalid, GraphifyLimitInvalid, RerankUnavailable];

    /// <summary>
    /// The codes the CLI answers with exit 1 (attention, spec §8.6): an unknown or ambiguous anchor, a refused input or
    /// path, sessions off, no map, a refused map or probe file, a regression, labeling refused, a review file or target that doesn't match, no draft, a repo outside the scope, a refused promote.
    /// The rest are tool errors (exit 2); 040 is exit 75 (Sindri maps it).
    /// </summary>
    public static IReadOnlySet<string> Attention { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        UnknownAnchor.Code, PathInWorktree.Code, QueryInvalid.Code, SessionsDisabled.Code, NoMap.Code, MapFileInvalid.Code, MapNodeUndefined.Code,
        ExportOutsideHome.Code, MapFileChanged.Code, ProbeFileInvalid.Code, NoProbeFile.Code, AmbiguousAnchor.Code, ProbesRegressed.Code,
        LabelingUnavailable.Code, ReviewFileNoMatch.Code, NoDraftMap.Code, RepoOutsideScope.Code, PromoteRefused.Code, ReviewTargetStale.Code, SyncRunning.Code, SyncSourceUnknown.Code,
        NoEligibleSample.Code, NoBriefSource.Code, UnknownArm.Code,
        CommitUnknown.Code, NoMergeBase.Code, MapVersionNotRetained.Code, CodeGlobInvalid.Code, CodeGlobsTooMany.Code, DraftChanged.Code, MapHeadInvalid.Code,
    };
}
