namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>A failed sign-in as GET /sindri/connections/{name} serves it beside a connected row.</summary>
/// <param name="Code">The error code (SND-CONN-009 for a denied consent, and so on).</param>
/// <param name="At">When it failed.</param>
internal sealed record ConnectionSignInFailure(string Code, DateTimeOffset At);
