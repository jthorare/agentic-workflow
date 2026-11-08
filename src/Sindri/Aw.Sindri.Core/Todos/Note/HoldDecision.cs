namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Whether a pass holds the changes it counted (todo spec §4.5 "The hold", REQ-E7).</summary>
/// <param name="Held">True when the counted changes are held and none applies.</param>
/// <param name="Changes">The counted changes, as given.</param>
public sealed record HoldDecision(bool Held, IReadOnlyList<HeldChange> Changes)
{
    private const int MaxApplied = 3;
    private const int MaxAppliedPercent = 10;

    /// <summary>Decides. Held only when there are more than 3 counted changes AND they are more than 10% of the base's open todos (owner, 2026-11-07; 10% is provisional). Exactly 10% is not more than 10%.</summary>
    /// <param name="counted">The removals and edits of open todos and the reopened done ones the note made; ticks, adds and changes to lines that stay '[x]' are not counted by the caller.</param>
    /// <param name="baseOpenTodos">How many open todos the base has.</param>
    /// <returns>The decision.</returns>
    public static HoldDecision Decide(IReadOnlyList<HeldChange> counted, int baseOpenTodos)
    {
        ArgumentNullException.ThrowIfNull(counted);
        var held = counted.Count > MaxApplied && counted.Count * 100 > baseOpenTodos * MaxAppliedPercent;
        return new HoldDecision(held, counted);
    }
}
