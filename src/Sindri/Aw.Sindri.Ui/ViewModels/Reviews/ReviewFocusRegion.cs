namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Where focus goes on the review page (review page spec §6.4).</summary>
internal enum ReviewFocusRegion
{
    /// <summary>Nowhere in particular.</summary>
    None,

    /// <summary>The summary strip's first segment.</summary>
    StripFirst,

    /// <summary>The heading of a whole-page state (not found, offline, failed), where focus goes after a Try again that failed.</summary>
    StateHeading,

    /// <summary>A panel's header or row; the key is the panel id, or "panelId:rowId" for a row.</summary>
    Panel,

    /// <summary>A diff line; the key is "path:+line" for an added line, "path:-line" for a removed one, "path:line" for an unchanged one.</summary>
    DiffLine,

    /// <summary>A diff file's header, when the file has no line to focus (hidden, binary, too large, a pure rename); the key is its path.</summary>
    FileNote,

    /// <summary>A diff group's header; the key is its name.</summary>
    GroupHeader,

    /// <summary>The fix-task sheet's text box.</summary>
    FixText,

    /// <summary>The fix-task sheet's outcome text.</summary>
    FixOutcome,
}
