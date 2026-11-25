using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The session collector's counts from the last status (<see cref="HealthViewModel"/>, continued; U11a T5).</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>The status' session counts; null before the first status and when the status serves none (before CO-3).</summary>
    internal SessionCountsView? Sessions { get; private set => Set(ref field, value); }

    /// <summary>The Agents card's problems, one line each with its code (#4012); empty while the count is 0.</summary>
    public IReadOnlyList<AgentProblemLine> AgentsProblemLines { get; private set => Set(ref field, value); } = [];

    /// <summary>True while the Agents card counts a problem: its lines and the link to the Agents page show (#4012).</summary>
    public bool HasAgentsProblems { get; private set => Set(ref field, value); }

    /// <summary>The Agents card's link to the Agents page.</summary>
    public string AgentsLinkText { get; } = Labels.AgentsOpen;

    /// <summary>The Agents card's link was pressed; the shell opens the Agents page.</summary>
    internal event EventHandler? AgentsRequested;

    /// <summary>Asks the shell to open the Agents page.</summary>
    internal void RequestAgents() => AgentsRequested?.Invoke(this, EventArgs.Empty);
}
