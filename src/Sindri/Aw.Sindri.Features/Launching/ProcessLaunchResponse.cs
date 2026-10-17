namespace Aw.Sindri.Features.Launching;

/// <summary>How a request ended.</summary>
/// <param name="Launched">True when a session was started and 'launch.started' won the outcome key.</param>
public sealed record ProcessLaunchResponse(bool Launched);
