using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.SessionHeartbeat;

/// <summary>The SessionHeartbeat response.</summary>
/// <param name="Launch">The launch the heartbeat was for.</param>
public sealed record SessionHeartbeatResponse(LaunchId Launch);
