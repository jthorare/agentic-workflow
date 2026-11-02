namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The one start-to-close bound of the workflow's activities that have no natural end (agent launch and close, input fills, gates, cleanup, effects, bookkeeping):
/// an hour, far above any real run, so a slow box or a cold provider never turns a working step into a failed one. Owner q93, 2026-10-23: no short fixed caps.
/// </summary>
internal static class ActivityBounds
{
    /// <summary>One hour.</summary>
    internal static readonly TimeSpan Long = TimeSpan.FromHours(1);
}
