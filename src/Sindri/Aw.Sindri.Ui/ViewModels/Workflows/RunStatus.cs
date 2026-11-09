namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>What the page reads from a run's served status word.</summary>
internal static class RunStatus
{
    /// <summary>True while the run has not ended: running, waiting or queued. Its age counts from the start; an ended run's counts from its end.</summary>
    /// <param name="status">The served status.</param>
    /// <returns>True when the run is live.</returns>
    internal static bool IsLive(string status) =>
        string.Equals(status, "running", StringComparison.Ordinal) || string.Equals(status, "waiting", StringComparison.Ordinal) || string.Equals(status, "queued", StringComparison.Ordinal);
}
