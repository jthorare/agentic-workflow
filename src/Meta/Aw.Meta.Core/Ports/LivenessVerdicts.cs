using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>How a caller reads one answer of <see cref="IProcessLiveness.Running"/>.</summary>
public static class LivenessVerdicts
{
    /// <summary>
    /// Whether to keep the claimed process in a list of live ones: running, or unknown because the host could not be asked about
    /// it. An unknown pid is kept (a session or a lock that is wrongly dropped is worse than one wrongly kept for a moment) and one
    /// pid that can't be checked never fails the whole list.
    /// </summary>
    /// <param name="verdict">The answer for one claim.</param>
    /// <returns>True when running or unknown.</returns>
    public static bool IsLiveOrUnknown(this Result<bool> verdict) => verdict.IsError || verdict.Value;
}
