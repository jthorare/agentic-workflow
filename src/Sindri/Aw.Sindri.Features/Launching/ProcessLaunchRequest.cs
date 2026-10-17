using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.Launching;

/// <summary>One request to launch (UI spec E-1).</summary>
/// <param name="Grant">The lease the outcome is appended under.</param>
/// <param name="Pending">The request.</param>
public sealed record ProcessLaunchRequest(LeaseGrant Grant, PendingLaunch Pending);
