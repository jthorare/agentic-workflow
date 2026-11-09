using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Core.Todos;

/// <summary>Who runs a pick and when (todo spec D2: any caller class may pick).</summary>
/// <param name="Caller">The caller.</param>
/// <param name="Grant">The lease grant the control plane holds.</param>
/// <param name="Now">Sindri's clock when the pick began.</param>
public sealed record TodoPickCaller(CallerContext Caller, LeaseGrant Grant, DateTimeOffset Now)
{
    /// <summary>The 'via' of the pick's events: 'pipe' when the request came with fence=1, else null.</summary>
    public string? Via { get; init; }

    /// <summary>The 'by' of the pick's events.</summary>
    public string By => TodoWrite.By(Caller);
}
