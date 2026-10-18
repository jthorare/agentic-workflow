using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.SessionHeartbeat;

/// <summary>'POST /sessions/{launch}/heartbeat' (API spec §8.1, class issue): 'session wrap' says the session is alive, proven by the launch secret.</summary>
/// <param name="LaunchId">The launch.</param>
/// <param name="Secret">The launch secret (header X-Sindri-Launch-Secret).</param>
/// <param name="SessionId">The agent's own session id, once wrap has read it (launcher L-9), otherwise null.</param>
public sealed record SessionHeartbeatRequest(LaunchId LaunchId, LaunchSecret Secret, SessionId? SessionId = null) : IIssue<SessionHeartbeatResponse>;
