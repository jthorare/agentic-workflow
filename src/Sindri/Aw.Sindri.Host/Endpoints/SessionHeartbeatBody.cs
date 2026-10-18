using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/sessions/{launchId}/heartbeat: '{sessionId?}'; the launch comes from the route and the proof from X-Sindri-Launch-Secret.</summary>
/// <param name="SessionId">The agent's own session id once wrap has read it (launcher L-9), otherwise absent.</param>
internal sealed record SessionHeartbeatBody(SessionId? SessionId = null);
