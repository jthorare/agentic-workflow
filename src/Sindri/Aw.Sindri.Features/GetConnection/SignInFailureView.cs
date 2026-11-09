namespace Aw.Sindri.Features.GetConnection;

/// <summary>A failed sign-in on the wire: its error code (SND-CONN-003, -004, -009, ...) and when it failed.</summary>
/// <param name="Code">The error code.</param>
/// <param name="At">When it failed.</param>
public sealed record SignInFailureView(string Code, DateTimeOffset At);
