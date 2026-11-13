using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context:map' row before the first map (#3216). A first run has no map file to import: the map builds by itself once a mapped repo's code index has succeeded (#3126).
/// In order: a map build runs; context-map-refresh is paused; the newest map build failed; no mapped repo has an index yet; else the next tick builds it.
/// </summary>
// why: red stub (#3216), only the tests call it until the fix wires it into FirstMapDoctorCheck
#pragma warning disable MA0182
internal static class FirstMapRow
#pragma warning restore MA0182
{
    /// <summary>The row.</summary>
    /// <param name="repos">The profile's mapped repos.</param>
    /// <param name="paused">Whether context-map-refresh is paused.</param>
    /// <param name="mapJobs">The RefreshContextMap and BuildContextMap jobs, any state.</param>
    /// <param name="indexed">The repos with a succeeded code index build.</param>
    /// <returns>The row.</returns>
    internal static DoctorCheck Of(IReadOnlyList<string> repos, bool paused, IReadOnlyList<JobRow> mapJobs, IReadOnlySet<string> indexed)
    {
        _ = (repos, paused, mapJobs, indexed);   // red (#3216): today's words
        return new("context:map", CheckState.Warn, "no map yet", "sindri context map import <file>");
    }
}
