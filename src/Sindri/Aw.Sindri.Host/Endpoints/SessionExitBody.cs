using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/sessions/{launchId}/exit: '{exitCode, sessionId?}'; the launch comes from the route and the proof from X-Sindri-Launch-Secret.</summary>
/// <param name="ExitCode">The agent's exit code.</param>
/// <param name="SessionId">The agent's own session id when the last heartbeat didn't carry it, otherwise absent.</param>
internal sealed record SessionExitBody(int ExitCode, SessionId? SessionId = null);
