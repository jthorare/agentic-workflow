using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>How the agent ended, as wrap tells the control plane.</summary>
/// <param name="ExitCode">The agent's exit code.</param>
/// <param name="Session">The agent's session id if found, otherwise null.</param>
/// <param name="Limit">The sandbox limit that made wrap kill the agent, or null for an ordinary exit.</param>
internal sealed record WrapExit(int ExitCode, SessionId? Session = null, LimitEnd? Limit = null);
