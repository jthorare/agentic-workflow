namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>The drawn state glyph at the start of a row (#2978 grammar).</summary>
internal enum RowGlyph
{
    /// <summary>An open to-do.</summary>
    Todo,

    /// <summary>Work under way.</summary>
    Doing,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>A draft.</summary>
    Draft,

    /// <summary>Waiting on a review.</summary>
    InReview,

    /// <summary>Waiting on the person.</summary>
    NeedsYou,

    /// <summary>Failed.</summary>
    Failed,

    /// <summary>Parked.</summary>
    Parked,

    /// <summary>An agent is running.</summary>
    AgentRunning,

    /// <summary>A message.</summary>
    Message,

    /// <summary>A mention.</summary>
    Mention,

    /// <summary>An open pull request.</summary>
    PrOpen,

    /// <summary>A draft pull request.</summary>
    PrDraft,

    /// <summary>A merged pull request.</summary>
    Merged,

    /// <summary>A CI run in progress.</summary>
    CiRunning,

    /// <summary>A linked record.</summary>
    Linked,
}
