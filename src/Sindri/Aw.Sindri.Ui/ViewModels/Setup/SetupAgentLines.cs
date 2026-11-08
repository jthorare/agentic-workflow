using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The agents step's lines (Claude Code, Codex, Cursor) from the repo lines the API serves.</summary>
internal static class SetupAgentLines
{
    // Claude Code, Codex and Cursor. A server that serves the providers decides each one; an older one is read from the repo lines' detail: connected when every line names the
    // agent, connecting while the step runs, else not started.
    internal static List<SetupAgentViewModel> For(IReadOnlyList<SetupItemView> items, bool running)
    {
        var served = items.Any(i => i.Providers is not null);
        return
        [
            .. Labels.SetupAgentNames.Select(agent =>
            {
                var (word, chip) = AgentWords(served ? ServedState(items, agent) : DetailState(items, agent, running));
                return new SetupAgentViewModel(agent, word, new StateChip(chip.Word, chip.Tone));
            }),
        ];
    }

    private enum AgentState { NotStarted, Connecting, Connected, Failed }

    private static (string Word, (string Word, string Said, ChipTone Tone) Chip) AgentWords(AgentState state) => state switch
    {
        AgentState.Connected => (Labels.SetupAgentConnected, Labels.SetupChip("done")),
        AgentState.Failed => (Labels.SetupAgentFailed, Labels.SetupChip("failed")),
        AgentState.Connecting => (Labels.SetupAgentConnecting, Labels.SetupChip("running")),
        AgentState.NotStarted => (Labels.SetupAgentNotStarted, Labels.SetupChip("todo")),
        _ => (Labels.SetupAgentNotStarted, Labels.SetupChip("todo")),
    };

    private static AgentState DetailState(IReadOnlyList<SetupItemView> items, string agent, bool running) =>
        items.Count > 0 && items.All(i => Names(i.Detail, agent)) ? AgentState.Connected : running ? AgentState.Connecting : AgentState.NotStarted;

    // The provider's state on every repo line: failed anywhere wins, connected needs every line, connecting anywhere shows, a line that doesn't list it leaves it not started.
    private static AgentState ServedState(IReadOnlyList<SetupItemView> items, string agent)
    {
        var states = items.Select(i => i.Providers?.FirstOrDefault(p => string.Equals(Labels.SetupAgentForProvider(p.Provider), agent, StringComparison.Ordinal))?.State).ToList();
        if (states.Contains("failed", StringComparer.Ordinal))
        {
            return AgentState.Failed;
        }

        if (states.Count > 0 && states.TrueForAll(st => string.Equals(st, "connected", StringComparison.Ordinal)))
        {
            return AgentState.Connected;
        }

        return states.Contains("connecting", StringComparer.Ordinal) ? AgentState.Connecting : AgentState.NotStarted;
    }

    private static bool Names(string? detail, string agent) =>
        detail is not null && detail.Split(',', StringSplitOptions.TrimEntries).Contains(agent, StringComparer.OrdinalIgnoreCase);
}
