namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Where a fix task comes from (review page spec §8.2).</summary>
internal enum ReviewFixKind
{
    /// <summary>A failed required check: its thread is a comment in the review body.</summary>
    Check,

    /// <summary>An unclaimed requirement: its thread is a comment in the review body.</summary>
    Requirement,

    /// <summary>A diff line: its thread is a line comment.</summary>
    Line,
}
