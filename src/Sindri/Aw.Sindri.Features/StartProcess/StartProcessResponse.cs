using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.StartProcess;

/// <summary>The lease this process now holds, the approved profile it runs with, and the Temporal dev server beside it.</summary>
/// <param name="Grant">The grant.</param>
/// <param name="Profile">The approved profile hash, if any.</param>
/// <param name="Temporal">The dev server it started or adopted (recorded in control.json).</param>
public sealed record StartProcessResponse(LeaseGrant Grant, ProfileHash? Profile, TemporalEndpoint Temporal);
