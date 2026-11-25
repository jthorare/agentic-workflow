using Aw.Kernel;

namespace Aw.Meta.Core;

/// <summary>The Meta error catalogue (codes AW-META-NNN).</summary>
public static class MetaErrors
{
    /// <summary>AW-META-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-META-001", ErrorKind.Invalid, "Meta input failed validation");

    /// <summary>AW-META-002.</summary>
    public static readonly ErrorDefinition NoTeamConfig = new("AW-META-002", ErrorKind.NotFound, "No team config; run aw-meta team-init");

    /// <summary>AW-META-003.</summary>
    public static readonly ErrorDefinition ConfigInvalid = new("AW-META-003", ErrorKind.Invalid, "The team config is not valid");

    /// <summary>AW-META-004.</summary>
    public static readonly ErrorDefinition TranscriptUnreadable = new("AW-META-004", ErrorKind.Unavailable, "A transcript can't be read");

    /// <summary>AW-META-005.</summary>
    public static readonly ErrorDefinition MessageNotFound = new("AW-META-005", ErrorKind.NotFound, "No such message");

    /// <summary>AW-META-006.</summary>
    public static readonly ErrorDefinition QuestionNotFound = new("AW-META-006", ErrorKind.NotFound, "No such owner question");

    /// <summary>AW-META-007.</summary>
    public static readonly ErrorDefinition GateStepNotFound = new("AW-META-007", ErrorKind.NotFound, "No such gate step");

    /// <summary>AW-META-008.</summary>
    public static readonly ErrorDefinition LogUnwritable = new("AW-META-008", ErrorKind.Unavailable, "The running log can't be written");

    /// <summary>AW-META-009.</summary>
    public static readonly ErrorDefinition CommandNotStarted = new("AW-META-009", ErrorKind.Unavailable, "A command could not be started");

    /// <summary>AW-META-010: a host figure or tool is unavailable on this OS (the process table, memory, a macOS tool, setsid or perl for the detached recycle waiter).</summary>
    public static readonly ErrorDefinition HostUnreadable = new("AW-META-010", ErrorKind.Unavailable, "The process table can't be read");

    /// <summary>AW-META-011.</summary>
    public static readonly ErrorDefinition ConfigExists = new("AW-META-011", ErrorKind.Conflict, "A team config already exists");

    /// <summary>AW-META-012.</summary>
    public static readonly ErrorDefinition CommandFailed = new("AW-META-012", ErrorKind.Unavailable, "An external command failed");

    /// <summary>AW-META-013.</summary>
    public static readonly ErrorDefinition LogUnreadable = new("AW-META-013", ErrorKind.Unavailable, "A step log can't be read");

    /// <summary>AW-META-014.</summary>
    public static readonly ErrorDefinition LaneVariablesUnset = new("AW-META-014", ErrorKind.Invalid, "AW_META_TEAM and AW_META_LANE are not both set: this is not a lane session");

    /// <summary>AW-META-015.</summary>
    public static readonly ErrorDefinition LaneNotRecorded = new("AW-META-015", ErrorKind.NotFound, "The lane has no recorded session");

    /// <summary>AW-META-016.</summary>
    public static readonly ErrorDefinition UnknownLane = new("AW-META-016", ErrorKind.NotFound, "No such lane");

    /// <summary>AW-META-017.</summary>
    public static readonly ErrorDefinition ClaudeLane = new("AW-META-017", ErrorKind.Invalid, "A Claude lane: send with SendMessage");

    /// <summary>AW-META-018.</summary>
    public static readonly ErrorDefinition QueueEntryNotFound = new("AW-META-018", ErrorKind.NotFound, "No merge-queue entry for that pull request");

    /// <summary>AW-META-019.</summary>
    public static readonly ErrorDefinition TeamAmbiguous = new("AW-META-019", ErrorKind.Invalid, "Several teams exist and none is selected");

    /// <summary>AW-META-020.</summary>
    public static readonly ErrorDefinition TeamMigrationRefused = new("AW-META-020", ErrorKind.Conflict, "The legacy team can't be migrated");

    /// <summary>AW-META-021.</summary>
    public static readonly ErrorDefinition RecycleRefused = new("AW-META-021", ErrorKind.Conflict, "The lane can't be recycled");

    /// <summary>AW-META-022.</summary>
    public static readonly ErrorDefinition RecycleTimedOut = new("AW-META-022", ErrorKind.Unavailable, "The recycled lane never reported a new session");

    /// <summary>AW-META-023.</summary>
    public static readonly ErrorDefinition BoardContended = new("AW-META-023", ErrorKind.Conflict, "The team board kept changing under the write");

    /// <summary>AW-META-024.</summary>
    public static readonly ErrorDefinition NoRequiredChecks = new("AW-META-024", ErrorKind.Conflict, "The base branch has no required checks, so a CI gate has nothing to read");

    /// <summary>AW-META-025.</summary>
    public static readonly ErrorDefinition WorktreeRootInRepo = new("AW-META-025", ErrorKind.Invalid, "The worktree root is inside the repo");

    /// <summary>AW-META-026.</summary>
    public static readonly ErrorDefinition WorktreeHomeUnknown = new("AW-META-026", ErrorKind.Invalid, "The home directory is unknown, so the worktree root can't be placed");

    /// <summary>AW-META-027.</summary>
    public static readonly ErrorDefinition EventInvalid = new("AW-META-027", ErrorKind.Invalid, "An event needs a dotted lower-case kind and its fields as one JSON object");

    /// <summary>AW-META-028.</summary>
    public static readonly ErrorDefinition StackExtensionMissing = new("AW-META-028", ErrorKind.Conflict, "The pull request is stacked and the gh stack extension is not installed");

    /// <summary>Every Meta error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [Invalid, NoTeamConfig, ConfigInvalid, TranscriptUnreadable, MessageNotFound, QuestionNotFound, GateStepNotFound, LogUnwritable, CommandNotStarted, HostUnreadable, ConfigExists, CommandFailed, LogUnreadable, LaneVariablesUnset, LaneNotRecorded, UnknownLane, ClaudeLane, QueueEntryNotFound, TeamAmbiguous, TeamMigrationRefused, RecycleRefused, RecycleTimedOut, BoardContended, NoRequiredChecks, WorktreeRootInRepo, WorktreeHomeUnknown, EventInvalid, StackExtensionMissing];
}
