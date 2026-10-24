namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One choice of a scoping question: an offered answer with the reason it is offered, "Other", or "Skip".</summary>
internal sealed class ScopingChoiceViewModel : ObservableObject
{
    private readonly ScopingQuestionViewModel _question;

    internal ScopingChoiceViewModel(ScopingQuestionViewModel question, ScopingChoiceKind kind, string optionId, string text, string rationale, bool recommended)
    {
        (_question, Kind, OptionId, Text, Rationale, IsRecommended) = (question, kind, optionId, text, rationale, recommended);
        Name = recommended ? Labels.ScopingRecommendedName(text) : text;
    }

    /// <summary>What kind of choice it is.</summary>
    internal ScopingChoiceKind Kind { get; }

    /// <summary>The offered answer's id; empty for Other and Skip.</summary>
    internal string OptionId { get; }

    /// <summary>The choice in words.</summary>
    public string Text { get; }

    /// <summary>Why the answer is offered; empty for Other and Skip.</summary>
    public string Rationale { get; }

    /// <summary>True when there is a reason to show.</summary>
    public bool HasRationale => Rationale.Length > 0;

    /// <summary>True for the answer the interview recommends.</summary>
    public bool IsRecommended { get; }

    /// <summary>The word that marks the recommended answer.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string RecommendedWord => Labels.ScopingRecommended;
#pragma warning restore CA1822, MA0041

    /// <summary>The choice's accessible name.</summary>
    public string Name { get; }

    /// <summary>True while this is the person's choice for its question.</summary>
    public bool IsChosen { get; internal set => Set(ref field, value); }

    /// <summary>Makes this the question's choice.</summary>
    internal void Choose() => _question.Choose(this);
}
