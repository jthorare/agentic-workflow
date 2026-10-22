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

    /// <summary>SND-API-002: the control-plane process didn't answer in time (the ping within 2 s, connecting within 5 s, or the call itself within AW_SINDRI_CLI_TIMEOUT when that is set).</summary>
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

    /// <summary>SND-ONYX-007: the Onyx login given isn't an admin (it can't list groups or make a key).</summary>
    public static readonly ErrorDefinition OnyxNotAdmin = new("SND-ONYX-007", ErrorKind.Forbidden, "The Onyx login given isn't an admin");

    /// <summary>SND-ONYX-008: logging in, minting Sindri's key or verifying it failed.</summary>
    public static readonly ErrorDefinition OnyxKeyFailed = new("SND-ONYX-008", ErrorKind.Unavailable, "Minting or verifying Sindri's Onyx key failed");

    /// <summary>SND-ONYX-009: the key couldn't be stored (neither the OS secret store nor the owner-only file).</summary>
    public static readonly ErrorDefinition OnyxSecretNotStored = new("SND-ONYX-009", ErrorKind.Unavailable, "Writing the Onyx key to a secret store failed");

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

    /// <summary>SND-SVC-404: no service of the approved profile, and no built-in, has that name (API spec §9).</summary>
    public static readonly ErrorDefinition ServiceNotFound = new("SND-SVC-404", ErrorKind.NotFound, "No such service in the approved profile");

    /// <summary>SND-LAUNCH-001: the terminal viewer didn't open the launch; on a tmux host the session keeps running (API P10a, UI E-5).</summary>
    public static readonly ErrorDefinition LaunchViewerFailed = new("SND-LAUNCH-001", ErrorKind.Unavailable, "The terminal didn't open the launch");

    /// <summary>SND-LAUNCH-002: no terminal viewer is available here (launcher L-3: 'no terminal viewer', not 'no Warp').</summary>
    public static readonly ErrorDefinition LaunchNoViewer = new("SND-LAUNCH-002", ErrorKind.Unavailable, "There is no terminal viewer here");

    /// <summary>SND-LAUNCH-003: the agent CLI isn't on the login PATH, or the target isn't in the catalog (detail 'not-in-catalog').</summary>
    public static readonly ErrorDefinition LaunchAgentMissing = new("SND-LAUNCH-003", ErrorKind.NotFound, "The agent CLI isn't available");

    /// <summary>SND-LAUNCH-004: the item's repo isn't on disk.</summary>
    public static readonly ErrorDefinition LaunchRepoMissing = new("SND-LAUNCH-004", ErrorKind.NotFound, "The repo isn't on disk");

    /// <summary>SND-LAUNCH-404: no launch has that id, or a session caller may not see it.</summary>
    public static readonly ErrorDefinition LaunchNotFound = new("SND-LAUNCH-404", ErrorKind.NotFound, "No such launch");

    /// <summary>SND-LAUNCH-006: there is no session to resume or attach to; the mode falls back to Open.</summary>
    public static readonly ErrorDefinition LaunchNoSession = new("SND-LAUNCH-006", ErrorKind.NotFound, "There is no session to resume or attach to");

    /// <summary>SND-LAUNCH-007: the launch was interrupted (Sindri restarted mid-launch, or the request passed its 30 s deadline); it is never retried on its own.</summary>
    public static readonly ErrorDefinition LaunchInterrupted = new("SND-LAUNCH-007", ErrorKind.Unavailable, "The launch was interrupted");

    /// <summary>SND-LAUNCH-008: a launch is already in progress for the item.</summary>
    public static readonly ErrorDefinition LaunchInProgress = new("SND-LAUNCH-008", ErrorKind.Conflict, "A launch is already in progress for the item");

    /// <summary>SND-LAUNCH-009: the launch (or its host start) timed out; a session or a window may still have opened.</summary>
    public static readonly ErrorDefinition LaunchTimedOut = new("SND-LAUNCH-009", ErrorKind.Timeout, "The launch timed out");

    /// <summary>SND-LAUNCH-010: the target's spend today reached its dailyBudget, or a budget is set on a target with no usage reader (detail 'no-usage-reader').</summary>
    public static readonly ErrorDefinition LaunchBudgetReached = new("SND-LAUNCH-010", ErrorKind.RateLimited, "The target's daily budget is reached");

    /// <summary>SND-LAUNCH-012: the agent entry isn't approved for unattended Steps (detail 'not-approved' or 'hash-changed'; launcher SL8).</summary>
    public static readonly ErrorDefinition LaunchNotApproved = new("SND-LAUNCH-012", ErrorKind.Forbidden, "This agent entry isn't approved for unattended Steps");

    /// <summary>SND-LAUNCH-015: the agent entry's session id pattern timed out on an id, so the id was refused unjudged (not as a mismatch). -014 is reserved by the sandbox spec.</summary>
    public static readonly ErrorDefinition LaunchSessionIdTimedOut = new("SND-LAUNCH-015", ErrorKind.Unavailable, "The session id pattern timed out");

    /// <summary>SND-LAUNCH-022: a Step's worktree already has a pre-push hook that isn't Sindri's; the launch is refused rather than overwrite it (line spec LN-LCH1).</summary>
    public static readonly ErrorDefinition LaunchPrePushForeign = new("SND-LAUNCH-022", ErrorKind.Conflict, "The Step's worktree has a pre-push hook of its own");

    /// <summary>SND-LAUNCH-023: printed by the pre-push hook Sindri installs when an agent Step tries to push; the push is refused (line spec LN-LCH1).</summary>
    public static readonly ErrorDefinition LaunchPushRefused = new("SND-LAUNCH-023", ErrorKind.Forbidden, "Agent Steps can't push; Sindri pushes");

    /// <summary>SND-LAUNCH-024: the agent CLI's trust file can't be updated for the Step's worktree (unparseable, locked or not writable); the file is left untouched and the launch is refused.</summary>
    public static readonly ErrorDefinition LaunchTrustUnavailable = new("SND-LAUNCH-024", ErrorKind.Unavailable, "The agent CLI's trust file couldn't be updated");

    /// <summary>SND-LAUNCH-013: a Step asked for a model or a policy, and the entry's matching form is 'none'; nothing launches (launcher L-17).</summary>
    public static readonly ErrorDefinition LaunchFormUnsupported = new("SND-LAUNCH-013", ErrorKind.Invalid, "The agent entry can't express the Step's model or policy");

    /// <summary>SND-AUTH-003: the launch secret doesn't match an open launch, or the run was taken over (API spec §8.1).</summary>
    public static readonly ErrorDefinition AuthLaunchSecret = new("SND-AUTH-003", ErrorKind.Forbidden, "The launch secret doesn't match an open launch");

    /// <summary>SND-LAUNCH-011: launcher.json or an agent entry in it isn't valid (launcher spec §4); the defaults are used.</summary>
    public static readonly ErrorDefinition LauncherInvalid = new("SND-LAUNCH-011", ErrorKind.Invalid, "launcher.json isn't valid");

    /// <summary>SND-SESSION-001: one of the collector's sources failed or overran in a pass; a problem row beside a normal list.</summary>
    public static readonly ErrorDefinition SessionSourceFailed = new("SND-SESSION-001", ErrorKind.Unavailable, "A session source failed in the last pass");

    /// <summary>SND-SESSION-002: the session collector can't run here (collector spec CA-5): unsupported-platform, proc-unreadable or disabled.</summary>
    public static readonly ErrorDefinition SessionsUnavailable = new("SND-SESSION-002", ErrorKind.Unavailable, "The session collector can't run here");

    /// <summary>SND-SESSION-003: a test override (AW_SESSIONS_PROC_ROOT, AW_SESSIONS_CLAUDE_DIR) is set in this run; informational, one problem row beside a normal list.</summary>
    public static readonly ErrorDefinition SessionsTestOverride = new("SND-SESSION-003", ErrorKind.Unavailable, "A test override is set in this run");

    /// <summary>SND-SESSION-404: no live session has that ref, or it is outside a session caller's repo (collector spec CA-5).</summary>
    public static readonly ErrorDefinition SessionNotFound = new("SND-SESSION-404", ErrorKind.NotFound, "No such live session");

    /// <summary>SND-SESSION-409: the session at that ref is a different process than the caller looked at (collector spec CA-4, CA-5, CA-9).</summary>
    public static readonly ErrorDefinition SessionConflict = new("SND-SESSION-409", ErrorKind.Conflict, "The session at that ref is a different process");

    /// <summary>SND-WT-001: a worktree request is not valid: the base is not a full 40-hex sha, the repo or path is not absolute, the branch is not usable, or the path is inside another worktree.</summary>
    public static readonly ErrorDefinition WorktreeInvalid = new("SND-WT-001", ErrorKind.Invalid, "The worktree request is not valid");

    /// <summary>SND-WT-002: the worktree path (or its branch) already exists at a different base or branch; nothing is reset, so the engine parks.</summary>
    public static readonly ErrorDefinition WorktreeConflict = new("SND-WT-002", ErrorKind.Conflict, "The worktree path or branch is already in use at a different base");

    /// <summary>SND-WT-003: git failed: the command failed, the repo is missing, or the commit is unknown.</summary>
    public static readonly ErrorDefinition WorktreeGitFailed = new("SND-WT-003", ErrorKind.Unavailable, "git could not create or remove the worktree");

    /// <summary>SND-WT-004: the worktree has uncommitted or untracked files; it is left as it is, never discarded.</summary>
    public static readonly ErrorDefinition WorktreeDirty = new("SND-WT-004", ErrorKind.Conflict, "The worktree has uncommitted changes");

    /// <summary>SND-SETUP-001: the folder to add is missing, is not a git checkout, is unreadable, is inside the Sindri home, is '/', is the home folder, or contains the Sindri home (setup-once spec §7.6).</summary>
    public static readonly ErrorDefinition SetupFolderInvalid = new("SND-SETUP-001", ErrorKind.Invalid, "That folder can't be added as a repo (fix: pick the repo's top folder)");

    /// <summary>SND-SETUP-002: the repo is already in the profile, or its name collides with another repo's.</summary>
    public static readonly ErrorDefinition SetupRepoExists = new("SND-SETUP-002", ErrorKind.Conflict, "That repo is already in the profile, or its name is taken (fix: pick another folder, or give a different name)");

    /// <summary>SND-SETUP-003: the app's confirmation was not given: a hold that was too short, an answer less than 2 seconds after the challenge, or the wrong kind of answer.</summary>
    public static readonly ErrorDefinition SetupConfirmationRefused = new("SND-SETUP-003", ErrorKind.Forbidden, "The confirmation was not given (fix: for a held one, hold the button for the full time or type the text; for a typed one, type the text exactly)");

    /// <summary>SND-SETUP-004: RegisterMcp for a repo the approved profile does not name.</summary>
    public static readonly ErrorDefinition SetupRepoNotInProfile = new("SND-SETUP-004", ErrorKind.Conflict, "The approved profile does not name that repo (fix: add the repo, approve the profile, then try again)");

    /// <summary>SND-SETUP-005: ConnectOnyx without the Onyx admin email and password.</summary>
    public static readonly ErrorDefinition SetupOnyxCredentialsMissing = new("SND-SETUP-005", ErrorKind.Invalid, "The Onyx admin email and password are both needed (fix: enter both; sign up in Onyx first if you have no account)");

    /// <summary>SND-SETUP-006: ConnectOnyx while no Onyx setup is waiting for the login, or the 30-minute wait ran out.</summary>
    public static readonly ErrorDefinition SetupOnyxNotWaiting = new("SND-SETUP-006", ErrorKind.Conflict, "No Onyx setup is waiting for the login (fix: press Set up Onyx first)");

    /// <summary>SND-SETUP-007: the approve-all plan, or its challenge, was asked for before a profile is approved (setup-once spec §7.6).</summary>
    public static readonly ErrorDefinition SetupProfileNotApproved = new("SND-SETUP-007", ErrorKind.Conflict, "No profile is approved yet (fix: approve the profile first)");

    /// <summary>SND-FORGE-001: a pull-request call while the approved profile names no forge ('workflow.forge.adapter' is absent).</summary>
    public static readonly ErrorDefinition NoForge = new("SND-FORGE-001", ErrorKind.Conflict, "No forge is configured (fix: set workflow.forge.adapter in the profile and approve it)");

    /// <summary>SND-FORGE-002: the forge port refused the call: a host outside 'workflow.forge.hosts', a missing or rejected credential, input the forge doesn't accept.</summary>
    public static readonly ErrorDefinition ForgeRefused = new("SND-FORGE-002", ErrorKind.Forbidden, "The forge refused the call");

    /// <summary>SND-FORGE-003: the forge has no such pull request.</summary>
    public static readonly ErrorDefinition ForgeNotFound = new("SND-FORGE-003", ErrorKind.NotFound, "The forge has no such pull request");

    /// <summary>SND-FORGE-004: the forge can't be reached, is rate limiting, or a push failed.</summary>
    public static readonly ErrorDefinition ForgeUnavailable = new("SND-FORGE-004", ErrorKind.Unavailable, "The forge is unavailable");

    /// <summary>Every Sindri error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        LeaseHeld, LeaseLost, LeaseStale, ProfileMissing, ProfileInvalid, ProfileHashMismatch, ProfileNeedsTerminal,
        ProfileNotApproved, ApprovalsUnavailable, LedgerSchema, LedgerUnavailable, NotRunning, ApiTimeout, PortInUse, ApiNoCode, ApiInternal,
        CallerUnknown, OriginRefused, CommandBusy, AuthTokenRequired, AuthRefused, UnknownVerb, StartFailed,
        InvalidInput, ServiceFailed, ViewsRebuilding, ViewsBehind, StreamsFull, ViewsStalled, ItemNotFound, ItemConflict, RequestInvalid, RequestsRateLimited, DecisionNotFound, DecisionInvalid, JobNotFound, JobConflict, JobInterrupted, JobsRateLimited, ServiceNotFound, OnyxDockerUnavailable, OnyxComposeMissing, OnyxNotEnoughResources, OnyxPinsMalformed, OnyxPortTaken, OnyxNotAdmin, OnyxKeyFailed, OnyxSecretNotStored, OnyxNotReady, OnyxRemoteDocker, OnyxComposeFailed, OnyxWorkspaceUnusable, SecretPointerInvalid, SecretUnresolved, SecretFileUnsafe,
        LaunchNotFound, LaunchViewerFailed, LaunchNoViewer, LaunchAgentMissing, LaunchRepoMissing, LaunchNoSession, LaunchInterrupted, LaunchInProgress, LaunchTimedOut, LaunchBudgetReached,
        LauncherInvalid, LaunchNotApproved, LaunchFormUnsupported, LaunchPrePushForeign, LaunchPushRefused, LaunchTrustUnavailable, LaunchSessionIdTimedOut, AuthLaunchSecret, SessionSourceFailed, SessionsUnavailable, SessionsTestOverride, SessionNotFound, SessionConflict,
        WorktreeInvalid, WorktreeConflict, WorktreeGitFailed, WorktreeDirty, SetupFolderInvalid, SetupRepoExists, SetupConfirmationRefused, SetupRepoNotInProfile, SetupOnyxCredentialsMissing, SetupOnyxNotWaiting, SetupProfileNotApproved,
        NoForge, ForgeRefused, ForgeNotFound, ForgeUnavailable,
    ];
}
