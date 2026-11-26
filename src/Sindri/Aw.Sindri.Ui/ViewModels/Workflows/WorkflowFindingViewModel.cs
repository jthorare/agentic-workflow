using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// One finding of the server's validation (#3803), in the server's own words: what is wrong and how to fix it. The app's only logic is which codes it has a fix button for:
/// GRAPH-PROMPT-001 opens the step's question field (the field the finding names, without "config.").
/// </summary>
internal sealed class WorkflowFindingViewModel : ObservableObject
{
    private const string ConfigPrefix = "config.";
    private const string PromptReadsNoData = "GRAPH-PROMPT-001";

    /// <summary>Wraps one finding as served.</summary>
    /// <param name="finding">The finding.</param>
    internal WorkflowFindingViewModel(WorkflowFindingView finding)
    {
        (Id, Code, Node, Sentence, FixLine) = (finding.Id, finding.Code, finding.Node, finding.Message, finding.Fix);
        IsError = string.Equals(finding.Severity, "error", StringComparison.Ordinal);
        FixField = string.Equals(finding.Code, PromptReadsNoData, StringComparison.Ordinal) && finding.Node is not null && finding.Field is { } field && field.StartsWith(ConfigPrefix, StringComparison.Ordinal) && field.Length > ConfigPrefix.Length
            ? field[ConfigPrefix.Length..]
            : null;
    }

    /// <summary>The finding's id: code, node and field.</summary>
    public string Id { get; }

    /// <summary>The finding's code, such as GRAPH-PROMPT-001.</summary>
    public string Code { get; }

    /// <summary>The step the finding belongs to, or null for one about the whole workflow.</summary>
    public string? Node { get; }

    /// <summary>What is wrong: the server's message, verbatim.</summary>
    public string Sentence { get; }

    /// <summary>How to fix it: the server's fix line, verbatim.</summary>
    public string FixLine { get; }

    /// <summary>True for an error, false for a warning.</summary>
    public bool IsError { get; }

    /// <summary>The chip tone: error, or attention for a warning (the tones the canvas mark uses too).</summary>
    public string ToneWord => IsError ? "error" : "attention";

    /// <summary>The chip word: "Error" or "Warning".</summary>
    public string Word => Labels.WfFindingWord(IsError);

    /// <summary>The finding read as one text: its kind, the sentence and the fix line.</summary>
    public string Name => Labels.WfFindingName(IsError, Sentence, FixLine);

    /// <summary>The config field the fix opens (such as "prompt"), or null when the app has no fix for this finding.</summary>
    public string? FixField { get; }

    /// <summary>True when the app has a fix button for this finding and the editor can change the workflow (an archived one is read-only: no button, #4380).</summary>
    public bool HasFix => FixField is not null && CanFix;

    private bool CanFix { get; set; } = true;

    /// <summary>Turns the fix button on or off with the editor: a read-only workflow has none.</summary>
    /// <param name="canEdit">True when the editor can change the workflow.</param>
    internal void AllowFix(bool canEdit)
    {
        if (CanFix != canEdit)
        {
            CanFix = canEdit;
            OnChanged(nameof(HasFix));
        }
    }

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The fix button's words.</summary>
    public string FixText => Labels.WfFindingFix;
#pragma warning restore CA1822, MA0041
}
