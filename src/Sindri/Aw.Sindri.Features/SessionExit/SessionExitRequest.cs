using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.SessionExit;

/// <summary>'POST /sessions/{launch}/exit' (API spec §8.1, class issue): 'session wrap' says the agent exited with a code, proven by the launch secret.</summary>
/// <param name="LaunchId">The launch.</param>
/// <param name="Secret">The launch secret (header X-Sindri-Launch-Secret).</param>
/// <param name="ExitCode">The agent's exit code.</param>
/// <param name="SessionId">The agent's own session id, if wrap read one the last heartbeat didn't carry, otherwise null.</param>
public sealed record SessionExitRequest(LaunchId LaunchId, LaunchSecret Secret, int ExitCode, SessionId? SessionId = null) : IIssue<SessionExitResponse>;
