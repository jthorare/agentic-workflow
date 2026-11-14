
namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The form's fixed words (<see cref="NewAgentFormViewModel"/>, continued), as properties the view binds.</summary>
#pragma warning disable MA0182 // why: the Agents page wires the form with its view (DG2)
internal sealed partial class NewAgentFormViewModel
{
#pragma warning disable CA1822, MA0041 // why: the view binds these as instance properties
    /// <summary>The sheet's heading.</summary>
    public string HeadingText => Labels.NewAgentHeading;

    /// <summary>The Prompt field's label.</summary>
    public string PromptLabel => Labels.NewAgentPromptLabel;

    /// <summary>The Repo field's label.</summary>
    public string RepoLabel => Labels.NewAgentRepoLabel;

    /// <summary>The Agent field's label.</summary>
    public string AgentLabel => Labels.NewAgentAgentLabel;

    /// <summary>The switch's label.</summary>
    public string WorktreeLabel => Labels.NewAgentWorktreeLabel;

    /// <summary>The Esc hint's key cap.</summary>
    public string EscText => Labels.NewAgentEsc;

    /// <summary>The agents to choose from.</summary>
    public IReadOnlyList<AgentChoice> Agents => AgentChoice.All;

    /// <summary>The Show it link's words.</summary>
    public string ShowItText => Labels.NewAgentShowIt;

    /// <summary>The Copy command button's words.</summary>
    public string CopyCommandText => Labels.NewAgentCopyCommand;

    /// <summary>The Retry button's words.</summary>
    public string RetryText => Labels.NewAgentRetry;
#pragma warning restore CA1822, MA0041
}
