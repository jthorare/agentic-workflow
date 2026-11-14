namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>GET /sindri/sandbox/status (wire contract item 4): the last Check again result plus the config. Cheap, never probes.</summary>
/// <param name="Blockers">The blockers the last check found.</param>
/// <param name="Mechanism">'bubblewrap', 'seatbelt' or 'none'.</param>
/// <param name="Groups">The allowed host group names.</param>
/// <param name="Limits">The limits in force.</param>
/// <param name="Login">The model login source.</param>
/// <param name="Learn">The learn windows open now; empty when none.</param>
internal sealed record SandboxStatusView(
    IReadOnlyList<SandboxBlockerLineView> Blockers,
    string Mechanism,
    IReadOnlyList<string> Groups,
    SandboxLimitsView Limits,
    string Login,
    IReadOnlyList<SandboxLearnView> Learn)
{
    /// <summary>When the last check ran; null means never checked (and then Ready is null too). The server leaves the key out before the first check (#2810).</summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>True when the last check found no blocker; null when never checked. Left out by the server before the first check (#2810).</summary>
    public bool? Ready { get; init; }
}
