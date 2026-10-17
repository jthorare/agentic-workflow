using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The preflight's checks, in order docker, compose, memory, disk, port, and the port setup would use.</summary>
/// <param name="Checks">The five checks.</param>
/// <param name="Port">3000, or the first free fallback when 3000 is taken, or 3000 when none is free.</param>
public sealed record PreflightReport(IReadOnlyList<PreflightCheck> Checks, int Port)
{
    /// <summary>Gets a value indicating whether every check is ok.</summary>
    public bool Ok => Checks.All(static c => c.State == CheckState.Ok);
}
