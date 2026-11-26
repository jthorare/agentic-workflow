using System.Globalization;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run's side of one step.</summary>
internal sealed class CompareCellViewModel
{
    internal CompareCellViewModel()
    {
        Present = false;
        Message = "Not in this run";
    }

    internal CompareCellViewModel(NodeRunView run)
    {
        Present = true;
        var (glyph, word) = Labels.WfChip(run.Status);
        (StatusGlyph, StatusWord, ToneWord) = (glyph, word, RunStatus.ToneWord(run.Status));
        DurationText = run.Seconds is { } s and >= 0 ? (s < 10 ? string.Create(CultureInfo.InvariantCulture, $"{s:0.#} s") : Labels.WfDuration(s)) : string.Empty;
        var output = WorkflowJson.Pretty(run.Output);
        var input = WorkflowJson.Pretty(run.Input);
        (BodyLabel, Text) = output.Length > 0 ? ("Output", output) : input.Length > 0 ? ("Input", input) : (string.Empty, string.Empty);
        Message = run.Status switch
        {
            "failed" => run.Error ?? string.Empty,
            "skipped" => run.Error is { Length: > 0 } why ? "Did not run: " + why : "Did not run",
            _ => string.Empty,
        };
        Status = run.Status;
    }

    internal string Status { get; } = string.Empty;

    internal string Text { get; } = string.Empty;

    /// <summary>False when the run has no such step.</summary>
    public bool Present { get; }

    /// <summary>"Succeeded", "Failed", "Skipped"; empty when the step is not in the run.</summary>
    public string StatusWord { get; } = string.Empty;

    /// <summary>The status glyph.</summary>
    public string StatusGlyph { get; } = string.Empty;

    /// <summary>The chip tone word.</summary>
    public string ToneWord { get; } = "neutral";

    /// <summary>"5.2 s".</summary>
    public string DurationText { get; } = string.Empty;

    /// <summary>"Output" or "Input"; empty when there are no lines.</summary>
    public string BodyLabel { get; } = string.Empty;

    /// <summary>The output (or input) as lines, each marked when the other run lacks it.</summary>
    public IReadOnlyList<CompareLineViewModel> Lines { get; private set; } = [];

    /// <summary>True when there are lines to draw.</summary>
    public bool HasLines => Lines.Count > 0;

    /// <summary>True when there is a message line.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>The one-line message: the error, "Did not run: ...", or "Not in this run".</summary>
    public string Message { get; }

    /// <summary>True when the other run's message differs.</summary>
    public bool MessageDiffers { get; private set; }

    /// <summary>True when the head status row shows (the step is in the run).</summary>
    public bool HasStatus => Present;

    internal void Mark(CompareCellViewModel other)
    {
        var theirs = other.Text.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
        Lines = Text.Length == 0 ? [] : [.. Text.Split('\n').Select(l => new CompareLineViewModel(l, other.Present && !theirs.Contains(l.Trim())))];
        MessageDiffers = !string.Equals(Message, other.Message, StringComparison.Ordinal);
    }
}
