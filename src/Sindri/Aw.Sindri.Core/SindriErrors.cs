using Aw.Kernel;

namespace Aw.Sindri.Core;

/// <summary>The Sindri error catalogue (codes SND-AREA-NNN, docs/csharp/sindri.md).</summary>
public static class SindriErrors
{
    /// <summary>SND-LEASE-001: another live process holds the host lease.</summary>
    public static readonly ErrorDefinition LeaseHeld = new("SND-LEASE-001", ErrorKind.Conflict, "The host lease is held by another process");

    /// <summary>SND-LEASE-002: this process no longer holds the lease (it expired and another took it).</summary>
    public static readonly ErrorDefinition LeaseLost = new("SND-LEASE-002", ErrorKind.Conflict, "The host lease was lost");

    /// <summary>SND-LEASE-003: a write carried an epoch that isn't the current one (fencing).</summary>
    public static readonly ErrorDefinition LeaseStale = new("SND-LEASE-003", ErrorKind.Conflict, "The lease epoch is not current");

    /// <summary>SND-PROFILE-001: no profile file.</summary>
    public static readonly ErrorDefinition ProfileMissing = new("SND-PROFILE-001", ErrorKind.NotFound, "No profile found");

    /// <summary>SND-PROFILE-002: the profile doesn't parse or doesn't match the schema.</summary>
    public static readonly ErrorDefinition ProfileInvalid = new("SND-PROFILE-002", ErrorKind.Invalid, "The profile is invalid");

    /// <summary>SND-PROFILE-003: the hash to approve isn't the live profile's hash.</summary>
    public static readonly ErrorDefinition ProfileHashMismatch = new("SND-PROFILE-003", ErrorKind.Conflict, "The hash doesn't match the profile");

    /// <summary>SND-PROFILE-004: approving needs a typed confirmation at an interactive terminal.</summary>
    public static readonly ErrorDefinition ProfileNeedsTerminal = new("SND-PROFILE-004", ErrorKind.Forbidden, "Approving a profile needs an interactive terminal");

    /// <summary>SND-PROFILE-005: no profile has been approved yet.</summary>
    public static readonly ErrorDefinition ProfileNotApproved = new("SND-PROFILE-005", ErrorKind.NotFound, "No profile is approved");

    /// <summary>SND-PROFILE-006: the approved snapshots can't be read or written (permissions, a full disk).</summary>
    public static readonly ErrorDefinition ApprovalsUnavailable = new("SND-PROFILE-006", ErrorKind.Unavailable, "The approvals can't be read or written");

    /// <summary>SND-LEDGER-001: the ledger's schema doesn't match this build.</summary>
    public static readonly ErrorDefinition LedgerSchema = new("SND-LEDGER-001", ErrorKind.Conflict, "The ledger schema doesn't match this build");

    /// <summary>SND-LEDGER-002: the ledger can't be opened or written.</summary>
    public static readonly ErrorDefinition LedgerUnavailable = new("SND-LEDGER-002", ErrorKind.Unavailable, "The ledger is unavailable");

    /// <summary>SND-API-001: the control-plane process isn't running.</summary>
    public static readonly ErrorDefinition NotRunning = new("SND-API-001", ErrorKind.Unavailable, "Sindri is not running");

    /// <summary>SND-API-002: the control-plane process didn't answer in time (the ping within 2 s, or the call itself within 10 s).</summary>
    public static readonly ErrorDefinition ApiTimeout = new("SND-API-002", ErrorKind.Timeout, "Sindri didn't answer in time");

    /// <summary>SND-API-003: the API's port is taken by another program.</summary>
    public static readonly ErrorDefinition PortInUse = new("SND-API-003", ErrorKind.Unavailable, "The API port is in use");

    /// <summary>SND-API-004: the API answered an error without a catalogue code (a route or method it doesn't serve, a body it couldn't read).</summary>
    public static readonly ErrorDefinition ApiNoCode = new("SND-API-004", ErrorKind.Unavailable, "The API answered without an error code");

    /// <summary>SND-API-005: a handler failed with an unexpected exception (a bug); the control plane logged it.</summary>
    public static readonly ErrorDefinition ApiInternal = new("SND-API-005", ErrorKind.Unavailable, "Internal error in the control plane");

    /// <summary>SND-CLI-001: an unknown verb or option.</summary>
    public static readonly ErrorDefinition UnknownVerb = new("SND-CLI-001", ErrorKind.Invalid, "Unknown command");

    /// <summary>SND-CLI-002: the process didn't come up after starting it.</summary>
    public static readonly ErrorDefinition StartFailed = new("SND-CLI-002", ErrorKind.Unavailable, "Sindri didn't start");

    /// <summary>SND-CLI-003: a command's input is malformed (a profile hash, AW_SINDRI_PORT).</summary>
    public static readonly ErrorDefinition InvalidInput = new("SND-CLI-003", ErrorKind.Invalid, "Invalid command input");

    /// <summary>SND-SCHED-001: a background service run failed.</summary>
    public static readonly ErrorDefinition ServiceFailed = new("SND-SCHED-001", ErrorKind.Unavailable, "A background service failed");

    /// <summary>SND-WF-001: a recipe fails validation (each problem names its check and path).</summary>
    public static readonly ErrorDefinition RecipeInvalid = new("SND-WF-001", ErrorKind.Invalid, "The recipe is invalid");

    /// <summary>SND-WF-002: no recipe with that id is registered, or it is invalid.</summary>
    public static readonly ErrorDefinition RecipeUnknown = new("SND-WF-002", ErrorKind.NotFound, "No such recipe");

    /// <summary>SND-WF-003: the instance's version isn't the one the caller expected (optimistic concurrency).</summary>
    public static readonly ErrorDefinition InstanceChanged = new("SND-WF-003", ErrorKind.Conflict, "The instance changed");

    /// <summary>SND-WF-004: no workflow instance with that id.</summary>
    public static readonly ErrorDefinition InstanceUnknown = new("SND-WF-004", ErrorKind.NotFound, "No such workflow instance");

    /// <summary>SND-WF-005: the instance can't take that signal or command in its current state.</summary>
    public static readonly ErrorDefinition TransitionNotAllowed = new("SND-WF-005", ErrorKind.Conflict, "The transition isn't allowed now");

    /// <summary>SND-WF-006: a gate refused the transition.</summary>
    public static readonly ErrorDefinition GateRefused = new("SND-WF-006", ErrorKind.Forbidden, "A gate refused the transition");

    /// <summary>SND-WF-007: the transition needs evidence that isn't registered for this instance and sha.</summary>
    public static readonly ErrorDefinition EvidenceMissing = new("SND-WF-007", ErrorKind.Forbidden, "Required evidence is missing");

    /// <summary>SND-WF-008: the recipe changed under a running instance (its hash isn't the one the instance started with).</summary>
    public static readonly ErrorDefinition RecipeChanged = new("SND-WF-008", ErrorKind.Conflict, "The recipe changed under the instance");

    /// <summary>SND-WF-009: a signal sets an output the current step doesn't declare, or a value of the wrong type.</summary>
    public static readonly ErrorDefinition OutputInvalid = new("SND-WF-009", ErrorKind.Invalid, "An output is not valid here");

    /// <summary>Every Sindri error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        LeaseHeld, LeaseLost, LeaseStale, ProfileMissing, ProfileInvalid, ProfileHashMismatch, ProfileNeedsTerminal,
        ProfileNotApproved, ApprovalsUnavailable, LedgerSchema, LedgerUnavailable, NotRunning, ApiTimeout, PortInUse, ApiNoCode, ApiInternal,
        UnknownVerb, StartFailed, InvalidInput, ServiceFailed, RecipeInvalid, RecipeUnknown, InstanceChanged, InstanceUnknown, TransitionNotAllowed,
        GateRefused, EvidenceMissing, RecipeChanged, OutputInvalid,
    ];
}
