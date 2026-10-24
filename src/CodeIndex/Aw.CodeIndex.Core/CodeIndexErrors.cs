using Aw.Kernel;

namespace Aw.CodeIndex.Core;

/// <summary>The code index's error catalogue (spec §12), codes SND-CODEIDX-NNN; 017 to 029 are reserved. Layer codes (006 to 009) are carried in a layer row and the build report and fail only the call that depends on the layer.</summary>
public static class CodeIndexErrors
{
    /// <summary>SND-CODEIDX-001: the 'index:' section has a bad key or value, or a repo name is not a file-safe name; the message starts with the key path.</summary>
    public static readonly ErrorDefinition SettingsInvalid = new("SND-CODEIDX-001", ErrorKind.Invalid, "The index settings are invalid");

    /// <summary>SND-CODEIDX-002: the repo path is not a git repo, git refuses it as unsafe, or the commit is not in it.</summary>
    public static readonly ErrorDefinition RepoNotFound = new("SND-CODEIDX-002", ErrorKind.NotFound, "The repo path is not a git repo, or the commit is not in it");

    /// <summary>SND-CODEIDX-003: the tracked input is over 'index.maxTotalMB'.</summary>
    public static readonly ErrorDefinition InputTooLarge = new("SND-CODEIDX-003", ErrorKind.Invalid, "The tracked code input is over the total size cap");

    /// <summary>SND-CODEIDX-004: every heavy-job slot stayed busy past the wait; only ever a job result code (the job ends deferred, not failed).</summary>
    public static readonly ErrorDefinition HeavyBusy = new("SND-CODEIDX-004", ErrorKind.Unavailable, "Every heavy-job slot stayed busy");

    /// <summary>SND-CODEIDX-005: the embedding URL is not loopback, or the model is a cloud model.</summary>
    public static readonly ErrorDefinition EmbeddingTargetRefused = new("SND-CODEIDX-005", ErrorKind.Invalid, "The embedding URL is not loopback, or the model is a cloud model");

    /// <summary>SND-CODEIDX-006: the embedding server failed or answered in an unexpected shape (a layer code).</summary>
    public static readonly ErrorDefinition EmbeddingServerFailed = new("SND-CODEIDX-006", ErrorKind.Unavailable, "The embedding server failed");

    /// <summary>SND-CODEIDX-007: no network sandbox for graphify (a layer code).</summary>
    public static readonly ErrorDefinition NoSandbox = new("SND-CODEIDX-007", ErrorKind.Unavailable, "No network sandbox for graphify");

    /// <summary>SND-CODEIDX-008: graphify is missing, failed, timed out, or wrote no usable or too large graph (a layer code).</summary>
    public static readonly ErrorDefinition GraphUnavailable = new("SND-CODEIDX-008", ErrorKind.Unavailable, "graphify is missing, failed, or wrote no usable graph");

    /// <summary>SND-CODEIDX-009: a tool or model does not match 'context/code-index.lock' (a layer code).</summary>
    public static readonly ErrorDefinition PinMismatch = new("SND-CODEIDX-009", ErrorKind.Unavailable, "A tool or model does not match the pins");

    /// <summary>SND-CODEIDX-010: the index file is damaged; it is deleted and rebuilt on the next tick.</summary>
    public static readonly ErrorDefinition IndexDamaged = new("SND-CODEIDX-010", ErrorKind.Unavailable, "The index file is damaged");

    /// <summary>SND-CODEIDX-011: the symbol ref names no symbol in this index (also a repo outside the caller's scope).</summary>
    public static readonly ErrorDefinition SymbolNotFound = new("SND-CODEIDX-011", ErrorKind.NotFound, "No symbol with this ref");

    /// <summary>SND-CODEIDX-012: a query is empty, too broad, or over a limit.</summary>
    public static readonly ErrorDefinition QueryInvalid = new("SND-CODEIDX-012", ErrorKind.Invalid, "The query is empty, too broad, or over a limit");

    /// <summary>SND-CODEIDX-013: no index for this repo yet (also a repo outside the caller's scope).</summary>
    public static readonly ErrorDefinition IndexNotBuilt = new("SND-CODEIDX-013", ErrorKind.NotFound, "No index for this repo yet");

    /// <summary>SND-CODEIDX-014: the index describes an older commit than the repo's main; only ever a health row, the index still answers.</summary>
    public static readonly ErrorDefinition IndexBehind = new("SND-CODEIDX-014", ErrorKind.Unavailable, "The index is behind main");

    /// <summary>SND-CODEIDX-015: a build failed and is retrying after its backoff; only ever a health row.</summary>
    public static readonly ErrorDefinition BuildFailed = new("SND-CODEIDX-015", ErrorKind.Unavailable, "An index build failed");

    /// <summary>SND-CODEIDX-016: the repo's index stayed locked by another build or embed run past the wait; the caller tries again later.</summary>
    public static readonly ErrorDefinition IndexBusy = new("SND-CODEIDX-016", ErrorKind.Unavailable, "The repo's index is locked by another build or embed run");

    /// <summary>Every code-index error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        SettingsInvalid, RepoNotFound, InputTooLarge, HeavyBusy, EmbeddingTargetRefused, EmbeddingServerFailed, NoSandbox,
        GraphUnavailable, PinMismatch, IndexDamaged, SymbolNotFound, QueryInvalid, IndexNotBuilt, IndexBehind, BuildFailed, IndexBusy,
    ];
}
