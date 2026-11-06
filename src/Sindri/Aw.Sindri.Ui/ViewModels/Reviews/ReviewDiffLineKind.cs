namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>What a diff line is.</summary>
internal enum ReviewDiffLineKind
{
    /// <summary>An unchanged line (name: "line 41: text").</summary>
    Context,

    /// <summary>An added line, drawn with a "+" (name: "line 42, added: text").</summary>
    Added,

    /// <summary>A removed line, drawn with a "-" (name: "line 40, removed: text").</summary>
    Removed,
}
