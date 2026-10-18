using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The limit that killed an agent Step's session (sandbox spec §6, -019).</summary>
/// <param name="Kind">Memory or wall.</param>
/// <param name="Limit">The limit: bytes for memory, seconds for wall.</param>
/// <param name="Peak">The most the session used, in the same unit.</param>
public sealed record AgentSessionLimit(LimitKind Kind, long Limit, long Peak);
