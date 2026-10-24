namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What kind of choice a button on a scoping question is.</summary>
internal enum ScopingChoiceKind
{
    /// <summary>One of the answers the interview offers.</summary>
    Option,

    /// <summary>The person's own answer, written in a box.</summary>
    Other,

    /// <summary>Leave the question unanswered.</summary>
    Skip,
}
