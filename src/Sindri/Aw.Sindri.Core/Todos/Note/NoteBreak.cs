namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Why a task line of the Obsidian note can't be a todo, by the text rules of D3 (todo spec §1, §4.5). When several apply, the first in this order is reported.</summary>
public enum NoteBreak
{
    /// <summary>The line is a valid todo.</summary>
    None,

    /// <summary>A second '^td-' id sits in the text (the trailing one is the line's id).</summary>
    TwoIds,

    /// <summary>A second due marker with a date sits in the text (the trailing one is the due date).</summary>
    TwoDueMarkers,

    /// <summary>A due or done marker that isn't a trailing marker with a date, or a '^td-' that isn't a trailing id, sits in the text.</summary>
    MarkerInText,

    /// <summary>The text holds a markdown link that is not a trailing 'sindri://' link to an item, a PR or a Linear issue (any other kind, a workflow included, counts as a pasted link).</summary>
    ForeignLink,

    /// <summary>The text holds a character of the Control or Format categories or a line or paragraph separator.</summary>
    ForbiddenCharacter,

    /// <summary>The text is empty.</summary>
    EmptyText,

    /// <summary>The text is over 280 characters.</summary>
    TextTooLong,

    /// <summary>The line ends in a block id of its own ('^abc123') that isn't a '^td-' id; a second id on the line would break links to it ('[[note#^abc123]]'), so the line stays as text. Checked last.</summary>
    ForeignBlockId,
}
