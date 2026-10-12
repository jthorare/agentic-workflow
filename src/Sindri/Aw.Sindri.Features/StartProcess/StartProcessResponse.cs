using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.StartProcess;

/// <summary>The lease this process now holds and the approved profile it runs with.</summary>
/// <param name="Grant">The grant.</param>
/// <param name="Profile">The approved profile hash, if any.</param>
public sealed record StartProcessResponse(LeaseGrant Grant, ProfileHash? Profile);
