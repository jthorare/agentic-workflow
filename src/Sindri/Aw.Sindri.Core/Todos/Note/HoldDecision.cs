namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Whether a pass holds the changes it counted (todo spec §4.5 "The hold", REQ-E7).</summary>
/// <param name="Held">True when the counted changes are held and none applies.</param>
/// <param name="Changes">The counted changes, as given.</param>
public sealed record HoldDecision(bool Held, IReadOnlyList<HeldChange> Changes)
{
    /// <summary>Decides. Held when there are more than 3 counted changes, or, with 6 or more open todos in the base, more than half of them (the half rule can't bind once the first has not fired, since half of 6 is 3; it is kept as the spec words it).</summary>
    /// <param name="counted">The removals and edits of open todos and the reopened done ones the note made; ticks, adds and changes to lines that stay '[x]' are not counted by the caller.</param>
    /// <param name="baseOpenTodos">How many open todos the base has.</param>
    /// <returns>The decision.</returns>
    public static HoldDecision Decide(IReadOnlyList<HeldChange> counted, int baseOpenTodos) => throw new NotSupportedException();
}
