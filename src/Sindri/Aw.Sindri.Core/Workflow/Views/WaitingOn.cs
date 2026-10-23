namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Who an instance waits on, as the list row says it: a person, an agent or the engine.</summary>
public static class WaitingOn
{
    /// <summary>A person: a desk answer, a park, an escalation or a takeover.</summary>
    public const string You = "you";

    /// <summary>An agent session runs the open Step.</summary>
    public const string Agent = "agent";

    /// <summary>Neither: the engine is working or about to.</summary>
    public const string Engine = "engine";

    // The chip words that mean the human has the next move.
    private static readonly HashSet<string> _yourWords = new([StateChip.Decision, StateChip.Parked, StateChip.Escalated, StateChip.WithYou], StringComparer.Ordinal);

    /// <summary>Who the instance waits on.</summary>
    /// <param name="chip">The row's state chip.</param>
    /// <param name="deskOpen">Whether the frame has an open desk run.</param>
    /// <param name="agentRunOpen">Whether an agent step run is open.</param>
    /// <param name="ended">Whether the instance ended.</param>
    /// <returns><see cref="You"/> for DECISION, PARKED, ESCALATED, WITH YOU or an open desk run, <see cref="Agent"/> for an open agent run, <see cref="Engine"/> otherwise, null once it ended.</returns>
    public static string? Of(StateChip chip, bool deskOpen, bool agentRunOpen, bool ended) => ended
        ? null
        : deskOpen || _yourWords.Contains(chip.Word)
            ? You
            : agentRunOpen ? Agent : Engine;
}
