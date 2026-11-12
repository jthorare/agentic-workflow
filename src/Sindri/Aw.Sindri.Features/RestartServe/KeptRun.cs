namespace Aw.Sindri.Features.RestartServe;

/// <summary>An open agent run the restart leaves running: a session with a launch instance, the filter scripts/lib/sindri-open-runs.sh uses.</summary>
/// <param name="Ref">The session, '&lt;provider&gt;/&lt;id&gt;'.</param>
/// <param name="Instance">The workflow instance address.</param>
/// <param name="LaunchState">The launch's state, or null.</param>
public sealed record KeptRun(string Ref, string Instance, string? LaunchState);
