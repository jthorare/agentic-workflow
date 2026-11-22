using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context:map' row before the first map (#3216). A first run has no map file to import: the map builds by itself once a mapped repo's code index has succeeded (#3126).
/// In order: a map build runs; context-map-refresh is paused; the newest map build failed; no mapped repo has an index yet; else the next tick builds it.
/// </summary>
internal static class FirstMapRow
{
    /// <summary>The row.</summary>
    /// <param name="repos">The profile's mapped repos.</param>
    /// <param name="paused">Whether context-map-refresh is paused.</param>
    /// <param name="mapJobs">The RefreshContextMap and BuildContextMap jobs, any state.</param>
    /// <param name="indexed">The repos with a succeeded code index build.</param>
    /// <param name="leftOut">The profile's repos the map doesn't wait on: their checkout is off its default branch (#3216).</param>
    /// <returns>The row.</returns>
    internal static DoctorCheck Of(IReadOnlyList<string> repos, bool paused, IReadOnlyList<JobRow> mapJobs, IReadOnlySet<string> indexed, IReadOnlyList<string>? leftOut = null)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(mapJobs);
        ArgumentNullException.ThrowIfNull(indexed);
        if (mapJobs.Any(static j => j.State is JobState.Running or JobState.Waiting))
        {
            return Row(CheckState.Ok, "building the first map", fix: null);
        }

        if (paused)
        {
            return Row(CheckState.Warn, "no map yet: context-map-refresh is paused, so the first map doesn't build", "sindri services resume context-map-refresh");
        }

        // why: a job cancelled before it built says nothing about the build, so it must not hide an earlier failure (#3216); a later build that ended does
        if (mapJobs.Where(static j => j.EndedAt is not null && j.State != JobState.Cancelled).MaxBy(static j => j.EndedAt) is { State: JobState.Failed } failed)
        {
            var code = failed.Code is { } c ? $" ({c})" : string.Empty;
            return Row(CheckState.Warn, $"no map yet: the first map build failed{code}", $"sindri context map build, or Build map on the Context page (the failed job: sindri jobs show {failed.Id.Value})");
        }

        if (repos.Count == 0 && leftOut is { Count: > 0 })
        {
            return Row(CheckState.Warn, $"no map yet: no mapped repo is on its default branch ({string.Join(", ", leftOut)})", "check out the default branch in one of them");
        }

        if (!repos.Any(indexed.Contains))
        {
            return Row(CheckState.Ok, repos.Count == 0 ? "no map yet: the profile names no repo to map" : $"no map yet: the first map builds after the code index of {string.Join(", ", repos)}", fix: null);
        }

        return Row(CheckState.Ok, "no map yet: the first map builds on context-map-refresh's next tick", fix: null);
    }

    private static DoctorCheck Row(CheckState state, string detail, string? fix) => new("context:map", state, detail, fix);
}
