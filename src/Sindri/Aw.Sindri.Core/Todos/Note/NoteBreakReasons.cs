namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The words the sync line and the doctor row use for a line that breaks D3 ("line 14 can't sync: it has a markdown link").</summary>
public static class NoteBreakReasons
{
    /// <summary>The reason, as the end of "line 14 can't sync: …".</summary>
    /// <param name="reason">The break; <see cref="NoteBreak.None"/> is refused.</param>
    /// <returns>For example "it has a markdown link".</returns>
    public static string Of(NoteBreak reason) => reason switch
    {
        NoteBreak.TwoIds => "it has two ids",
        NoteBreak.TwoDueMarkers => "it has two due dates",
        NoteBreak.MarkerInText => "it has a \U0001F4C5, \u2705 or ^td- marker inside the text",
        NoteBreak.ForeignLink => "it has a markdown link",
        NoteBreak.ForbiddenCharacter => "it has a character Sindri can't store",
        NoteBreak.EmptyText => "it has no text",
        NoteBreak.TextTooLong => "it is longer than 280 characters",
        NoteBreak.ForeignBlockId => "it ends in a block id (^…) of its own",
        NoteBreak.None => throw new ArgumentOutOfRangeException(nameof(reason), reason, "a line that is a valid todo has no reason"),
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "a line that is a valid todo has no reason"),
    };
}
