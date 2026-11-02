using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The limit that killed an agent Step's session (sandbox spec §6, -019).</summary>
/// <param name="Kind">Memory or wall.</param>
/// <param name="Limit">The limit: bytes for memory, seconds for wall.</param>
/// <param name="Peak">The most the session used, in the same unit.</param>
public sealed record AgentSessionLimit(AgentLimitKind Kind, long Limit, long Peak)
{
    /// <summary>The words a failed Step carries after its reason 'limit': 'memory limit 2147483648 bytes, peak 2300000000 bytes' (wall is in seconds).</summary>
    public string Detail => string.Create(CultureInfo.InvariantCulture, $"{(Kind == AgentLimitKind.Memory ? "memory" : "wall")} limit {Limit} {Unit}, peak {Peak} {Unit}");

    private string Unit => Kind == AgentLimitKind.Memory ? "bytes" : "seconds";
}
