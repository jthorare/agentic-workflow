using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One finding of the server's validation (#3803), in the server's own words: what is wrong and how to fix it. Stub until the slice lands.</summary>
internal sealed class WorkflowFindingViewModel
{
    /// <summary>Wraps one finding as served.</summary>
    /// <param name="finding">The finding.</param>
    internal WorkflowFindingViewModel(WorkflowFindingView finding) => Code = finding.Code;

    /// <summary>The finding's code, such as GRAPH-PROMPT-001.</summary>
    public string Code { get; }

    /// <summary>What is wrong: the server's message, verbatim.</summary>
    public string Sentence { get; } = string.Empty;

    /// <summary>How to fix it: the server's fix line, verbatim.</summary>
    public string FixLine { get; } = string.Empty;

    /// <summary>True for an error, false for a warning.</summary>
    public bool IsError { get; }

    /// <summary>The chip tone: error or attention.</summary>
    public string ToneWord { get; } = string.Empty;

    /// <summary>True when the app has a fix button for this code.</summary>
    public bool HasFix { get; }
}
