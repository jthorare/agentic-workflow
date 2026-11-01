using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.ViewModels.Controls;

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

    /// <summary>The Resume control (the catalog's ResumeJob row acting on this job) while it is parked on 'model-limit'; else null.</summary>
    public ControlViewModel? Resume { get; private set => Set(ref field, value); }

    private void DrawModels(JobView job)
    {
        IsPaused = string.Equals(job.WaitingOn, ModelLines.ModelLimit, StringComparison.Ordinal);
        PausedText = IsPaused ? job.Progress is [.., var last] ? last.Line : Labels.JobModelPausedFallback : string.Empty;
        ModelCalls = job.ModelCalls is { Count: > 0 } ? ModelLines.Calls(job, _clock) : [];
        ModelMoreText = job.ModelCallCount > (job.ModelCalls?.Count ?? 0) ? ModelLines.More(job) ?? string.Empty : string.Empty;
        OnChanged(nameof(HasModelCalls));
        OnChanged(nameof(HasModelMore));
        Resume = IsPaused ? Resume ?? ResumeFor() : null;
    }

    // Like CancelFor: a refusal is the control's own error, and the job is read again after the call either way.
    private ControlViewModel? ResumeFor() =>
        _catalog?.On("ResumeJob", Heading, async ct =>
        {
            if (_url is not { } url || Id.Length == 0)
            {
                return SindriUiErrors.Unreachable.With("Sindri isn't reachable yet");
            }

            var resumed = await Api.ResumeJob(url, Id, ct).ConfigureAwait(true);
            await Read(ct).ConfigureAwait(true);
            return resumed.Map(static r => new CommandAcceptedView(r.Accepted.CommandId, r.Accepted.Sequence));
        });
}
