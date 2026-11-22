namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>What a row of the flat diff list draws (#2776).</summary>
internal enum ReviewDiffRowKind
{
    /// <summary>A group's header button.</summary>
    Group,

    /// <summary>A file's header: its path, status, stat and note.</summary>
    File,

    /// <summary>A hunk's "@@" header.</summary>
    Hunk,

    /// <summary>One line of a hunk.</summary>
    Line,
}
