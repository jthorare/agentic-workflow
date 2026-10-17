namespace Aw.Sindri.Features.Launching;

/// <summary>What a tick found.</summary>
/// <param name="Due">The requests to process now, each handed out once; the host runs <see cref="ProcessLaunch"/> for them.</param>
public sealed record RunLaunchResponse(IReadOnlyList<PendingLaunch> Due);
