namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What a merge decided.</summary>
/// <param name="Intents">What to do in Sindri, in note order.</param>
/// <param name="LineEdits">What to do to the note: rewrites from Sindri, ids written onto adopted lines, stale copies dropped, removals from Sindri, and appended new todos.</param>
/// <param name="NextBase">The base to save: in base order, then the lines new to the base in note order. It is the note's task lines as merged and written, with the base lines of held, left-alone and conflicted ids kept as they were.</param>
/// <param name="Hold">What happened to the hold.</param>
/// <param name="HoldNow">The hold that is open after the pass, or null.</param>
/// <param name="Adopted">How many lines were adopted (minted or unknown id).</param>
/// <param name="Deferred">How many new lines were left for the next pass by the adoption cap.</param>
public sealed record NoteMergeResult(
    IReadOnlyList<NoteIntent> Intents,
    IReadOnlyList<NoteLineEdit> LineEdits,
    IReadOnlyList<string> NextBase,
    NoteHoldOutcome Hold,
    NoteHold? HoldNow,
    int Adopted,
    int Deferred);
