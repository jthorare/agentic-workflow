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

    /// <summary>SND-SECRET-001: a secret pointer has no scheme, an unknown scheme or a malformed target.</summary>
    public static readonly ErrorDefinition SecretPointerInvalid = new("SND-SECRET-001", ErrorKind.Invalid, "The secret pointer is invalid");

    /// <summary>SND-SECRET-002: a secret pointer doesn't resolve (unset variable, missing file or entry, missing tool, timeout).</summary>
    public static readonly ErrorDefinition SecretUnresolved = new("SND-SECRET-002", ErrorKind.Unavailable, "The secret doesn't resolve");

    /// <summary>SND-SECRET-003: a file: pointer names a file others may read or write, or something that isn't a regular file.</summary>
    public static readonly ErrorDefinition SecretFileUnsafe = new("SND-SECRET-003", ErrorKind.Forbidden, "The secret file is unsafe");

    /// <summary>SND-API-010: a command that reads views was refused while the views rebuild; it wasn't run.</summary>
    public static readonly ErrorDefinition ViewsRebuilding = new("SND-API-010", ErrorKind.Unavailable, "Views are rebuilding; the command wasn't run, retry");

    /// <summary>SND-API-011: the views didn't reach the sequence a caller waits for (X-Sindri-After, or a command's head) in time.</summary>
    public static readonly ErrorDefinition ViewsBehind = new("SND-API-011", ErrorKind.Unavailable, "The views didn't reach the sequence in time");

    /// <summary>SND-API-012: every event stream a caller may open is open (16, 4 held for the human; API spec §5.4).</summary>
    public static readonly ErrorDefinition StreamsFull = new("SND-API-012", ErrorKind.RateLimited, "Too many event streams are open; re-query every 5 s");

    /// <summary>SND-VIEW-001: the projector stalled on an event or can't run (views.db or the ledger can't be used); 'sindri views rebuild' clears an event stall or a views.db cause, 'sindri doctor' diagnoses a ledger cause.</summary>
    public static readonly ErrorDefinition ViewsStalled = new("SND-VIEW-001", ErrorKind.Unavailable, "The projector stalled on an event");

    /// <summary>SND-ITEM-404: no item has that id or alias, or a session caller may not see it (API spec §3.1).</summary>
    public static readonly ErrorDefinition ItemNotFound = new("SND-ITEM-404", ErrorKind.NotFound, "No such item");

    /// <summary>SND-ITEM-409: the item changed since the caller looked, or it is already closed (API spec §2.1).</summary>
    public static readonly ErrorDefinition ItemConflict = new("SND-ITEM-409", ErrorKind.Conflict, "The item changed since you looked");

    /// <summary>SND-REQ-001: a request has an unknown kind, no text, or more than 2,000 characters (API spec §9).</summary>
    public static readonly ErrorDefinition RequestInvalid = new("SND-REQ-001", ErrorKind.Invalid, "The request isn't valid");

    /// <summary>SND-REQ-002: too many open requests in the repo (20) or in all (50) (API spec §4.2).</summary>
    public static readonly ErrorDefinition RequestsRateLimited = new("SND-REQ-002", ErrorKind.RateLimited, "Too many open requests");

    /// <summary>SND-DEC-404: no decision has that id, or a session caller may not see it.</summary>
    public static readonly ErrorDefinition DecisionNotFound = new("SND-DEC-404", ErrorKind.NotFound, "No such decision");

    /// <summary>SND-DEC-001: the choice isn't one of the decision's options, or the note is too long.</summary>
    public static readonly ErrorDefinition DecisionInvalid = new("SND-DEC-001", ErrorKind.Invalid, "The decision's choice isn't valid");

    /// <summary>SND-ONYX-004: the Onyx pins are malformed, a downloaded file or image failed its pin, or a compose image has no pin.</summary>
    public static readonly ErrorDefinition OnyxPinsMalformed = new("SND-ONYX-004", ErrorKind.Invalid, "The Onyx pins are malformed or a download failed its pin");

    /// <summary>SND-ONYX-001: the Docker CLI is missing or its daemon isn't answering; the message says to install or start it.</summary>
    public static readonly ErrorDefinition OnyxDockerUnavailable = new("SND-ONYX-001", ErrorKind.Unavailable, "Docker is missing or not running");

    /// <summary>SND-ONYX-002: Compose v2 is missing.</summary>
    public static readonly ErrorDefinition OnyxComposeMissing = new("SND-ONYX-002", ErrorKind.Unavailable, "Docker Compose v2 is missing");

    /// <summary>SND-ONYX-003: not enough free disk or Docker memory; the message says how much is needed and where.</summary>
    public static readonly ErrorDefinition OnyxNotEnoughResources = new("SND-ONYX-003", ErrorKind.BudgetExceeded, "Not enough disk or Docker memory for Onyx");

    /// <summary>SND-ONYX-006: Onyx's port is taken by something not ours and no fallback port is free.</summary>
    public static readonly ErrorDefinition OnyxPortTaken = new("SND-ONYX-006", ErrorKind.Conflict, "Onyx's port is taken and no fallback port is free");

    /// <summary>SND-ONYX-005: Onyx didn't become ready in time, or answered with a version other than the pinned one.</summary>
    public static readonly ErrorDefinition OnyxNotReady = new("SND-ONYX-005", ErrorKind.Unavailable, "Onyx isn't ready or runs another version");

    /// <summary>SND-ONYX-010: Docker points at another machine; Sindri only runs Onyx on this one.</summary>
    public static readonly ErrorDefinition OnyxRemoteDocker = new("SND-ONYX-010", ErrorKind.Unavailable, "Docker points at another machine");

    /// <summary>SND-ONYX-011: a `docker compose` command failed; the message ends with the last lines it printed.</summary>
    public static readonly ErrorDefinition OnyxComposeFailed = new("SND-ONYX-011", ErrorKind.Unavailable, "A docker compose command failed");

    /// <summary>SND-ONYX-012: Sindri can't use its Onyx directory under the Sindri home (no permission to create or lock a file in it); the message names the directory.</summary>
    public static readonly ErrorDefinition OnyxWorkspaceUnusable = new("SND-ONYX-012", ErrorKind.Unavailable, "Sindri can't use its Onyx directory");

    /// <summary>SND-JOB-404: no job has that id, or a session caller may not see it (API spec §9).</summary>
    public static readonly ErrorDefinition JobNotFound = new("SND-JOB-404", ErrorKind.NotFound, "No such job");

    /// <summary>SND-JOB-409: a job of that kind and key is already running (the error carries 'runningJob'), or the job to cancel already ended.</summary>
    public static readonly ErrorDefinition JobConflict = new("SND-JOB-409", ErrorKind.Conflict, "The job conflicts with one that is running or ended");

    /// <summary>SND-JOB-001: the process stopped while the job ran; it appears only as a job's code, never as an HTTP response.</summary>
    public static readonly ErrorDefinition JobInterrupted = new("SND-JOB-001", ErrorKind.Unavailable, "The job was interrupted: the process stopped while it ran");

    /// <summary>SND-JOB-002: the most jobs are running (8); start again when one ends.</summary>
    public static readonly ErrorDefinition JobsRateLimited = new("SND-JOB-002", ErrorKind.RateLimited, "Too many jobs are running");

    /// <summary>SND-SVC-404: no service of the approved profile has that name (API spec §9).</summary>
    public static readonly ErrorDefinition ServiceNotFound = new("SND-SVC-404", ErrorKind.NotFound, "No such service in the approved profile");

    /// <summary>Every Sindri error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        LeaseHeld, LeaseLost, LeaseStale, ProfileMissing, ProfileInvalid, ProfileHashMismatch, ProfileNeedsTerminal,
        ProfileNotApproved, ApprovalsUnavailable, LedgerSchema, LedgerUnavailable, NotRunning, ApiTimeout, PortInUse, ApiNoCode, ApiInternal,
        CallerUnknown, OriginRefused, CommandBusy, AuthTokenRequired, AuthRefused, UnknownVerb, StartFailed,
        InvalidInput, ServiceFailed, ViewsRebuilding, ViewsBehind, StreamsFull, ViewsStalled, ItemNotFound, ItemConflict, RequestInvalid, RequestsRateLimited, DecisionNotFound, DecisionInvalid, JobNotFound, JobConflict, JobInterrupted, JobsRateLimited, ServiceNotFound, OnyxDockerUnavailable, OnyxComposeMissing, OnyxNotEnoughResources, OnyxPinsMalformed, OnyxPortTaken, OnyxNotReady, OnyxRemoteDocker, OnyxComposeFailed, OnyxWorkspaceUnusable, SecretPointerInvalid, SecretUnresolved, SecretFileUnsafe,
    ];
}
