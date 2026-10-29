using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The job sheet's model calls and its pause (<see cref="JobSheetViewModel"/>, continued; context model routing spec §8.2).</summary>
internal sealed partial class JobSheetViewModel
{
    /// <summary>The "Model calls" list: one row per try, in call order; empty when the job made no call.</summary>
    public IReadOnlyList<JobModelRowViewModel> ModelCalls { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the job made calls to list.</summary>
    public bool HasModelCalls => ModelCalls.Count > 0;

    /// <summary>"showing the last 200 of 205" when the job made more calls than the list holds; empty otherwise.</summary>
    public string ModelMoreText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the list is cut.</summary>
    public bool HasModelMore => ModelMoreText.Length > 0;

    /// <summary>True while the job is parked on 'model-limit': the sheet shows why and Resume above Progress.</summary>
    public bool IsPaused { get; private set => Set(ref field, value); }

    /// <summary>Why the job is paused (its last progress line); empty while it isn't.</summary>
    public string PausedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Resume button's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ResumeText => Labels.JobResume;

    /// <summary>The Resume button's accessible name: "Resume Build the context map".</summary>
    public string ResumeName => Labels.JobResumeName(Heading);
#pragma warning restore CA1822, MA0041

    /// <summary>Sends the resume of the open job and reads it again; a job no longer waiting is refused by Sindri and the read shows it as it is now.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the job was read again.</returns>
    internal async Task Resume(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Id.Length == 0)
        {
            return;
        }

        _ = await Api.ResumeJob(url, Id, cancellationToken).ConfigureAwait(true);   // a job that is no longer waiting is refused; either way the job is read again
        await Read(cancellationToken).ConfigureAwait(true);
    }

    private void DrawModels(JobView job)
    {
        IsPaused = string.Equals(job.WaitingOn, ModelLines.ModelLimit, StringComparison.Ordinal);
        PausedText = IsPaused ? job.Progress is [.., var last] ? last.Line : Labels.JobModelPausedFallback : string.Empty;
        ModelCalls = job.ModelCalls is { Count: > 0 } ? ModelLines.Calls(job, _clock) : [];
        ModelMoreText = job.ModelCallCount > (job.ModelCalls?.Count ?? 0) ? ModelLines.More(job) ?? string.Empty : string.Empty;
        OnChanged(nameof(HasModelCalls));
        OnChanged(nameof(HasModelMore));
        OnChanged(nameof(ResumeName));
    }
}
