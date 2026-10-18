using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The limit that killed an agent Step's session (sandbox spec §6, -019).</summary>
/// <param name="Kind">Memory or wall.</param>
/// <param name="Limit">The limit: bytes for memory, seconds for wall.</param>
/// <param name="Peak">The most the session used, in the same unit.</param>
public sealed record AgentSessionLimit(LimitKind Kind, long Limit, long Peak)
{
    /// <summary>The words a 'step.failed' reason carries: 'limit: memory (limit 2147483648, peak 2300000000)'; memory is in bytes and wall in seconds.</summary>
    public string Text => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"limit: {(Kind == LimitKind.Memory ? "memory" : "wall")} (limit {Limit}, peak {Peak})");
}
