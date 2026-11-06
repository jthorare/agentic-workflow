namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Where focus goes on the review page (review page spec §6.4).</summary>
internal enum ReviewFocusRegion
{
    /// <summary>Nowhere in particular.</summary>
    None,

    /// <summary>The summary strip's first segment.</summary>
    StripFirst,

    /// <summary>A panel's header or row; the key names it.</summary>
    Panel,

    /// <summary>A diff line; the key is "path:line".</summary>
    DiffLine,

    /// <summary>A diff group's header; the key is its name.</summary>
    GroupHeader,

    /// <summary>The fix-task sheet's text box.</summary>
    FixText,

    /// <summary>The fix-task sheet's outcome text.</summary>
    FixOutcome,
}
