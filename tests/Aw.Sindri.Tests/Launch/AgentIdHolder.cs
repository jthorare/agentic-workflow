using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Wraps an AgentId so the JSON tests can send a missing or null property.</summary>
public sealed record AgentIdHolder(AgentId Id);
