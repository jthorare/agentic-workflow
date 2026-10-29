namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>One live agent session. State, source and host kind are read as strings, so a word this build doesn't know renders as itself instead of failing the read.</summary>
/// <param name="Ref">Ref: provider/id, the session's address.</param>
/// <param name="Provider">Provider, as served.</param>
/// <param name="ProviderLabel">ProviderLabel, as served.</param>
/// <param name="Id">Id, as served.</param>
/// <param name="IdKnown">IdKnown: false for a pid-derived id.</param>
/// <param name="State">State: running, asking, idle or unknown.</param>
/// <param name="Asking">Asking, as served; null unless the session waits on the human.</param>
/// <param name="StateSource">StateSource: record, hook or transcript.</param>
/// <param name="Estimated">Estimated, as served.</param>
/// <param name="Repo">Repo, as served.</param>
/// <param name="Worktree">Worktree, as served.</param>
/// <param name="Host">Host, as served.</param>
/// <param name="Lane">Lane, as served.</param>
/// <param name="LaunchId">LaunchId: set when Sindri launched it.</param>
/// <param name="StartedAt">StartedAt, as served; null for Codex and Cursor rows.</param>
/// <param name="Since">Since, as served.</param>
/// <param name="Focus">Focus, as served.</param>
/// <param name="Title">Title, as served; null for a session caller.</param>
/// <param name="Pid">Pid, as served; null for a session caller.</param>
/// <param name="Launch">Launch: the Step an engine launch started it for; null when no launch row is known, all-null members for an attended launch.</param>
/// <param name="Sandbox">Sandbox: the session's sandbox state and memory; null (omitted) for a row with no Sindri launch.</param>
internal sealed record SessionRowView(
    string Ref,
    string Provider,
    string ProviderLabel,
    string Id,
    bool IdKnown,
    string State,
    SessionAskingView? Asking,
    string StateSource,
    bool Estimated,
    string? Repo,
    string? Worktree,
    SessionHostView Host,
    SessionLaneView? Lane,
    string? LaunchId,
    DateTimeOffset? StartedAt,
    DateTimeOffset Since,
    SessionFocusView Focus,
    string? Title,
    int? Pid,
    SessionLaunchView? Launch = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Sandbox.SessionSandboxView? Sandbox = null);
