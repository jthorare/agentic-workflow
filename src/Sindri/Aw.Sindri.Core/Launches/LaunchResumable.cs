using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>A finished session that can be resumed.</summary>
/// <param name="Target">The agent.</param>
/// <param name="SessionId">The agent's own session id.</param>
public sealed record LaunchResumable(AgentId Target, SessionId SessionId);
