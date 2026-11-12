using Aw.Kernel;

namespace Aw.BugFix.Core.Errors;

/// <summary>
/// The BugFix error catalogue (codes AW-BUGFIX-NNN). The CLI maps <see cref="ErrorKind.Invalid"/> and
/// <see cref="ErrorKind.NotFound"/> to exit 1 and every other kind (a rule refusal, an unavailable judge) to exit 3.
/// </summary>
public static class BugFixErrors
{
    /// <summary>AW-BUGFIX-001: an argument or input file failed validation.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-BUGFIX-001", ErrorKind.Invalid, "BugFix input failed validation");

    /// <summary>AW-BUGFIX-002: the state directory has no state file yet.</summary>
    public static readonly ErrorDefinition StateMissing = new("AW-BUGFIX-002", ErrorKind.NotFound, "No bug-fix state");

    /// <summary>AW-BUGFIX-003: the state file can't be read as a BugFix state.</summary>
    public static readonly ErrorDefinition StateCorrupt = new("AW-BUGFIX-003", ErrorKind.Invalid, "Unreadable bug-fix state");

    /// <summary>AW-BUGFIX-004: an input file (ticket, handoff) can't be read.</summary>
    public static readonly ErrorDefinition FileUnreadable = new("AW-BUGFIX-004", ErrorKind.Invalid, "Input file unreadable");

    /// <summary>AW-BUGFIX-005: the step isn't allowed in the current phase or status.</summary>
    public static readonly ErrorDefinition WrongPhase = new("AW-BUGFIX-005", ErrorKind.Conflict, "Step not allowed now");

    /// <summary>AW-BUGFIX-006: the investigation handoff doesn't support a fix.</summary>
    public static readonly ErrorDefinition HandoffRefused = new("AW-BUGFIX-006", ErrorKind.Conflict, "Handoff refused");

    /// <summary>AW-BUGFIX-007: the baseline run doesn't reproduce the bug.</summary>
    public static readonly ErrorDefinition NotReproduced = new("AW-BUGFIX-007", ErrorKind.Conflict, "Bug not reproduced");

    /// <summary>AW-BUGFIX-008: the check file isn't committed, is outside the repo or changed since the baseline.</summary>
    public static readonly ErrorDefinition CheckNotFrozen = new("AW-BUGFIX-008", ErrorKind.Conflict, "Check not frozen");

    /// <summary>AW-BUGFIX-009: the evidence wasn't written by run-test or run-ui for this bug fix, or was modified.</summary>
    public static readonly ErrorDefinition EvidenceRefused = new("AW-BUGFIX-009", ErrorKind.Conflict, "Evidence refused");

    /// <summary>AW-BUGFIX-010: a new attempt isn't allowed (cap, mode, unevaluated or resolving candidate).</summary>
    public static readonly ErrorDefinition AttemptRefused = new("AW-BUGFIX-010", ErrorKind.Conflict, "Attempt refused");

    /// <summary>AW-BUGFIX-011: the candidate can't be recorded (tree, branch, baseline, hypothesis, protected files).</summary>
    public static readonly ErrorDefinition CandidateRefused = new("AW-BUGFIX-011", ErrorKind.Conflict, "Candidate refused");

    /// <summary>AW-BUGFIX-012: the run-test command could pass without executing the check.</summary>
    public static readonly ErrorDefinition CommandRefused = new("AW-BUGFIX-012", ErrorKind.Conflict, "Check command refused");

    /// <summary>AW-BUGFIX-013: no candidate has that id.</summary>
    public static readonly ErrorDefinition UnknownCandidate = new("AW-BUGFIX-013", ErrorKind.NotFound, "Unknown candidate");

    /// <summary>AW-BUGFIX-014: the run can't be recorded for the candidate (recorded, kind, command, commit, broken).</summary>
    public static readonly ErrorDefinition RunRefused = new("AW-BUGFIX-014", ErrorKind.Conflict, "Run refused");

    /// <summary>AW-BUGFIX-015: the candidate can't be judged (no passing run, already judged).</summary>
    public static readonly ErrorDefinition JudgeRefused = new("AW-BUGFIX-015", ErrorKind.Conflict, "Judge refused");

    /// <summary>AW-BUGFIX-016: judge gave no usable verdict; nothing was recorded and a retry isn't a re-roll.</summary>
    public static readonly ErrorDefinition JudgeUnavailable = new("AW-BUGFIX-016", ErrorKind.Unavailable, "Judge unavailable");

    /// <summary>AW-BUGFIX-017: ui-evidence produced no usable run.</summary>
    public static readonly ErrorDefinition UiEvidenceUnavailable = new("AW-BUGFIX-017", ErrorKind.Unavailable, "ui-evidence unavailable");

    /// <summary>AW-BUGFIX-018: a git command failed (not a repository, unknown revision).</summary>
    public static readonly ErrorDefinition GitFailed = new("AW-BUGFIX-018", ErrorKind.Invalid, "git failed");

    /// <summary>AW-BUGFIX-019: the report step isn't allowed (already reported, candidate not eligible).</summary>
    public static readonly ErrorDefinition ReportRefused = new("AW-BUGFIX-019", ErrorKind.Conflict, "Report refused");

    /// <summary>AW-BUGFIX-020: a working tree has uncommitted or untracked changes.</summary>
    public static readonly ErrorDefinition DirtyTree = new("AW-BUGFIX-020", ErrorKind.Conflict, "Working tree not clean");

    /// <summary>AW-BUGFIX-021: another bugfix-state call holds the state directory.</summary>
    public static readonly ErrorDefinition StateBusy = new("AW-BUGFIX-021", ErrorKind.Conflict, "State busy");

    /// <summary>AW-BUGFIX-022: a file under the state directory or a log can't be written or read.</summary>
    public static readonly ErrorDefinition StorageFailed = new("AW-BUGFIX-022", ErrorKind.Unavailable, "Storage failed");

    /// <summary>AW-BUGFIX-023: the bug fix's digest.key is missing or malformed, so environment digests can't be compared.</summary>
    public static readonly ErrorDefinition DigestKeyMissing = new("AW-BUGFIX-023", ErrorKind.Unavailable, "Digest key missing");

    /// <summary>AW-BUGFIX-024: a resolved report has no usable sweep (the "Where else we checked" table is empty or malformed and the Prevention decision is not an existing guard or deferred).</summary>
    public static readonly ErrorDefinition SweepRefused = new("AW-BUGFIX-024", ErrorKind.Conflict, "Sweep refused");

    /// <summary>Every BugFix error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        Invalid, StateMissing, StateCorrupt, FileUnreadable, WrongPhase, HandoffRefused, NotReproduced, CheckNotFrozen,
        EvidenceRefused, AttemptRefused, CandidateRefused, CommandRefused, UnknownCandidate, RunRefused, JudgeRefused,
        JudgeUnavailable, UiEvidenceUnavailable, GitFailed, ReportRefused, DirtyTree, StateBusy, StorageFailed,
        DigestKeyMissing, SweepRefused,
    ];
}
