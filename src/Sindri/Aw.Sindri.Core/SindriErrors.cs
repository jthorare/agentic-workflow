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

    /// <summary>SND-API-003: serve can't listen on the API port: another program has it, it is refused, or the host failed to start listening.</summary>
    public static readonly ErrorDefinition PortInUse = new("SND-API-003", ErrorKind.Unavailable, "The API port is in use");

    /// <summary>SND-API-004: the API answered an error without a catalogue code (a route, method or Content-Type it doesn't serve).</summary>
    public static readonly ErrorDefinition ApiNoCode = new("SND-API-004", ErrorKind.Unavailable, "The API answered without an error code");

    /// <summary>SND-API-005: a handler failed with an unexpected exception (a bug); the control plane logged it.</summary>
    public static readonly ErrorDefinition ApiInternal = new("SND-API-005", ErrorKind.Unavailable, "Internal error in the control plane");

    /// <summary>SND-API-007: an unknown X-Sindri-Caller value (or a malformed X-Sindri-Repo).</summary>
    public static readonly ErrorDefinition CallerUnknown = new("SND-API-007", ErrorKind.Invalid, "The caller header isn't one Sindri knows");

    /// <summary>SND-API-008: a command carried an Origin header (no first-party client sends one; a web page might).</summary>
    public static readonly ErrorDefinition OriginRefused = new("SND-API-008", ErrorKind.Forbidden, "Commands refuse an Origin header");

    /// <summary>SND-API-009: the command gate wasn't acquired within 5 s; another command is running.</summary>
    public static readonly ErrorDefinition CommandBusy = new("SND-API-009", ErrorKind.Unavailable, "Sindri is busy with another command");

    /// <summary>SND-AUTH-001: a human caller without the valid per-start token (missing or stale).</summary>
    public static readonly ErrorDefinition AuthTokenRequired = new("SND-AUTH-001", ErrorKind.Forbidden, "A human caller needs the per-start token");

    /// <summary>SND-AUTH-002: the caller may not run the command's action class.</summary>
    public static readonly ErrorDefinition AuthRefused = new("SND-AUTH-002", ErrorKind.Forbidden, "This caller may not run the command");

    /// <summary>SND-CLI-001: an unknown verb or option.</summary>
    public static readonly ErrorDefinition UnknownVerb = new("SND-CLI-001", ErrorKind.Invalid, "Unknown command");

    /// <summary>SND-CLI-002: the process didn't come up after starting it.</summary>
    public static readonly ErrorDefinition StartFailed = new("SND-CLI-002", ErrorKind.Unavailable, "Sindri didn't start");

    /// <summary>SND-CLI-003: bad input: a malformed CLI argument or setting (a profile hash, AW_SINDRI_PORT), or an API request body or value the control plane refused (a 400).</summary>
    public static readonly ErrorDefinition InvalidInput = new("SND-CLI-003", ErrorKind.Invalid, "Invalid command input");

    /// <summary>SND-SCHED-001: a background service run failed.</summary>
    public static readonly ErrorDefinition ServiceFailed = new("SND-SCHED-001", ErrorKind.Unavailable, "A background service failed");

    /// <summary>SND-API-010: a command that reads views was refused while the views rebuild; it wasn't run.</summary>
    public static readonly ErrorDefinition ViewsRebuilding = new("SND-API-010", ErrorKind.Unavailable, "Views are rebuilding; the command wasn't run, retry");

    /// <summary>SND-API-011: the views didn't reach the sequence a caller waits for (X-Sindri-After, or a command's head) in time.</summary>
    public static readonly ErrorDefinition ViewsBehind = new("SND-API-011", ErrorKind.Unavailable, "The views didn't reach the sequence in time");

    /// <summary>SND-API-012: every event stream a caller may open is open (16, 4 held for the human; API spec §5.4).</summary>
    public static readonly ErrorDefinition StreamsFull = new("SND-API-012", ErrorKind.RateLimited, "Too many event streams are open; re-query every 5 s");

    /// <summary>SND-VIEW-001: the projector stalled on an event; 'sindri views rebuild' clears it.</summary>
    public static readonly ErrorDefinition ViewsStalled = new("SND-VIEW-001", ErrorKind.Unavailable, "The projector stalled on an event");

    /// <summary>SND-ITEM-404: no item has that id or alias, or a session caller may not see it (API spec §3.1).</summary>
    public static readonly ErrorDefinition ItemNotFound = new("SND-ITEM-404", ErrorKind.NotFound, "No such item");

    /// <summary>SND-ITEM-409: the item changed since the caller looked, or it is already closed (API spec §2.1).</summary>
    public static readonly ErrorDefinition ItemConflict = new("SND-ITEM-409", ErrorKind.Conflict, "The item changed since you looked");

    /// <summary>Every Sindri error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        LeaseHeld, LeaseLost, LeaseStale, ProfileMissing, ProfileInvalid, ProfileHashMismatch, ProfileNeedsTerminal,
        ProfileNotApproved, ApprovalsUnavailable, LedgerSchema, LedgerUnavailable, NotRunning, ApiTimeout, PortInUse, ApiNoCode, ApiInternal,
        CallerUnknown, OriginRefused, CommandBusy, AuthTokenRequired, AuthRefused, UnknownVerb, StartFailed,
        InvalidInput, ServiceFailed, ViewsRebuilding, ViewsBehind, StreamsFull, ViewsStalled, ItemNotFound, ItemConflict,
    ];
}
