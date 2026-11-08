using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One coding agent's line in the agents step: a badge, its name, and what Sindri reached (Connected, Connecting or Not started).</summary>
internal sealed class SetupAgentViewModel
{
    internal SetupAgentViewModel(string agent, string word, StateChip chip)
    {
        (Agent, ResultText, Chip) = (agent, word, chip);
        Name = Labels.SetupAgentName(agent, word);
    }

    /// <summary>The agent's name ("Claude Code").</summary>
    public string Agent { get; }

    /// <summary>What Sindri reached, in a word.</summary>
    public string ResultText { get; }

    /// <summary>The badge.</summary>
    public StateChip Chip { get; }

    /// <summary>The line's accessible name: "Codex, not started".</summary>
    public string Name { get; }
}
