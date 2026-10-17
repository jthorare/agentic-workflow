using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.Launching;

/// <summary>One tick of the launcher service (UI spec E-1): read the new requests, sweep the expired, poll the unconfirmed viewers.</summary>
/// <param name="Grant">The lease the tick appends under.</param>
public sealed record RunLaunchRequest(LeaseGrant Grant);
