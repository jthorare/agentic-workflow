namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>A launch's sandbox summary (wire contract item 2), as launch.started carries it. Paths are listed, contents never.</summary>
/// <param name="Mechanism">'bubblewrap', 'seatbelt' or 'pass-through'.</param>
/// <param name="PlanHash">The hash of the plan.</param>
/// <param name="Writable">The writable paths.</param>
/// <param name="Network">The allowed host group names.</param>
/// <param name="NetworkMode">'enforce' or 'learn'.</param>
/// <param name="Inner">Whether the agent CLI's own sandbox ran: 'on' or 'off'.</param>
/// <param name="Limits">The limits in force.</param>
/// <param name="Credentials">The GitHub credentials the session has.</param>
/// <param name="Login">The model login source.</param>
/// <param name="Attended">True for an attended session; false (omitted) for a Step.</param>
internal sealed record LaunchSandboxView(
    string Mechanism,
    string PlanHash,
    IReadOnlyList<string> Writable,
    IReadOnlyList<string> Network,
    string NetworkMode,
    string Inner,
    SandboxLimitsView Limits,
    LaunchCredentialsView Credentials,
    string Login,
    bool Attended = false);
