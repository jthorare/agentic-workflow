using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The run page of a run Run now just started (#3702): Starting until the server shows it, then "didn't start" with Retry once the runs model's deadline passes.</summary>
internal sealed partial class RunDetailViewModel
{
    private string? _timedOutId;   // the run whose start deadline passed: a read that still does not find it keeps saying so
    private string? _startingId;   // the run Run now just started: until the server shows it, the page says Starting instead of failing

    /// <summary>True when the run Run now started got no run row for 10 s: the page says it did not start and offers Retry.</summary>
    public bool DidNotStart { get; private set => Set(ref field, value); }

    /// <summary>"The run didn't start."</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string DidNotStartText => Labels.WfRunDidNotStart;

    /// <summary>The Retry button's words on a run that did not start.</summary>
    public string StartRetryText => Labels.WfRunStartRetry;

    /// <summary>What to do about a run that did not start.</summary>
    public string DidNotStartHint => Labels.WfRunDidNotStartHint;
#pragma warning restore CA1822, MA0041

    /// <summary>The runs model's start deadline passed for a run: when this page shows that Starting run, it says the run did not start. The deadline is the runs model's one timer; this page starts none.</summary>
    /// <param name="runId">The run whose start deadline passed.</param>
    internal void MarkDidNotStart(string runId)
    {
        if (!string.Equals(_startingId, runId, StringComparison.Ordinal))
        {
            return;
        }

        _timedOutId = runId;
        DidNotStart = string.Equals(Id, runId, StringComparison.Ordinal) && string.Equals(Status, "starting", StringComparison.Ordinal);
    }

    /// <summary>Marks a run Run now just started: until the server shows it, its page reads Starting, not "couldn't read the run".</summary>
    /// <param name="runId">The new run's id.</param>
    internal void ExpectStarting(string runId) => (_startingId, _timedOutId, DidNotStart) = (runId, null, false);

    /// <summary>The page was left: the run Run now started is no longer awaited (its deadline stopped with the page), so a later read of it is an ordinary read.</summary>
    internal void ForgetStarting()
    {
        (_startingId, _timedOutId, DidNotStart) = (null, null, false);
        if (string.Equals(Status, "starting", StringComparison.Ordinal))
        {
            Id = string.Empty;   // the next read starts clean: no Starting chip stays from the placeholder
        }
    }

    private void ShowStarting(string runId)
    {
        var (glyph, word) = Labels.WfChip("starting");
        (Id, TitleText, Status, Tone) = (runId, Labels.WfRunTitle(runId), "starting", ChipTone.Neutral);
        (ChipWord, ChipGlyph, ChipToneWord) = (word, glyph, "neutral");
        (IsFailed, FailedText) = (false, string.Empty);
        DidNotStart = string.Equals(_timedOutId, runId, StringComparison.Ordinal);
        Changed();
    }
}
