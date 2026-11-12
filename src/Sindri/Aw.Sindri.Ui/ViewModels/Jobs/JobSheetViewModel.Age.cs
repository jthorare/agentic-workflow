using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The job sheet's state line (<see cref="JobSheetViewModel"/>, continued): worded at the read and again as time passes.</summary>
internal sealed partial class JobSheetViewModel
{
    /// <summary>Words the state line's "Started 2m ago" again from the clock now, while a job is shown (the shell's one-second beat calls it).</summary>
    internal void Age()
    {
        if (_last is { } job && Phase == FramePhase.Shown)
        {
            StateLine = StateOf(job);
        }
    }

    private string StateOf(JobView job)
    {
        var running = string.Equals(job.State, "running", StringComparison.Ordinal) || string.Equals(job.State, "waiting", StringComparison.Ordinal);
        var (glyph, word) = Labels.JobChip(job.State);
        var age = _clock.GetUtcNow() - (running ? job.StartedAt : job.EndedAt ?? job.StartedAt);
        return string.Join(" · ", glyph + " " + word, Labels.JobWhen(running, age), "by " + Labels.JobStartedBy(job.StartedBy));
    }
}
