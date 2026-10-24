using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One question of the scoping interview: the choices (the offered answers, Other when allowed, Skip when allowed) and the person's pick.</summary>
internal sealed class ScopingQuestionViewModel : ObservableObject
{
    private readonly Action _changed;

    internal ScopingQuestionViewModel(ScopingQuestion question, Action changed)
    {
        ArgumentNullException.ThrowIfNull(question);
        (Question, _changed) = (question, changed);
        List<ScopingChoiceViewModel> choices = [.. question.Options.Select(o => new ScopingChoiceViewModel(this, ScopingChoiceKind.Option, o.Id, o.Answer, o.Rationale, o.Recommended))];
        if (question.Other)
        {
            choices.Add(new ScopingChoiceViewModel(this, ScopingChoiceKind.Other, optionId: string.Empty, Labels.ScopingOther, rationale: string.Empty, recommended: false));
        }

        if (question.Skip)
        {
            choices.Add(new ScopingChoiceViewModel(this, ScopingChoiceKind.Skip, optionId: string.Empty, Labels.ScopingSkip, rationale: string.Empty, recommended: false));
        }

        Choices = choices;
    }

    /// <summary>The served question.</summary>
    internal ScopingQuestion Question { get; }

    /// <summary>The question in words.</summary>
    public string Prompt => Question.Question;

    /// <summary>What the question is about; empty when not served.</summary>
    public string Dimension => Question.Dimension;

    /// <summary>True when the question says what it is about.</summary>
    public bool HasDimension => Dimension.Length > 0;

    /// <summary>The choices, in the order shown.</summary>
    public IReadOnlyList<ScopingChoiceViewModel> Choices { get; }

    /// <summary>The person's pick; null before one is made.</summary>
    internal ScopingChoiceViewModel? Picked { get; private set; }

    /// <summary>True while "Other" is the pick: its box shows.</summary>
    public bool IsOther => Picked?.Kind == ScopingChoiceKind.Other;

    /// <summary>The box's accessible name.</summary>
    public string OtherName => Labels.NeedsYouAnswerName(Question.Question);

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The box's placeholder.</summary>
    public string OtherPlaceholder => Labels.NeedsYouAnswerPlaceholder;
#pragma warning restore CA1822, MA0041

    /// <summary>What the person wrote under "Other".</summary>
    public string OtherText
    {
        get;
        set
        {
            var next = value ?? string.Empty;
            if (!string.Equals(field, next, StringComparison.Ordinal))
            {
                field = next;
                OnChanged(nameof(OtherText));
                OnChanged(nameof(IsAnswered));
                _changed();
            }
        }
    } = string.Empty;

    /// <summary>True when the question has a pick that counts: an offered answer, "Other" with words, or "Skip".</summary>
    public bool IsAnswered => Picked is { } picked && (picked.Kind != ScopingChoiceKind.Other || !string.IsNullOrWhiteSpace(OtherText));

    /// <summary>True when the question was left to "Skip".</summary>
    internal bool IsSkipped => Picked?.Kind == ScopingChoiceKind.Skip;

    /// <summary>Makes a choice the pick.</summary>
    /// <param name="choice">One of this question's choices.</param>
    internal void Choose(ScopingChoiceViewModel choice)
    {
        foreach (var each in Choices)
        {
            each.IsChosen = ReferenceEquals(each, choice);
        }

        Picked = choice;
        OnChanged(nameof(IsOther));
        OnChanged(nameof(IsAnswered));
        _changed();
    }

    /// <summary>This question's entry in a batch: {question, option, text}, {question, other} or {question, skip: true}; null before an answer counts.</summary>
    /// <returns>The entry.</returns>
    internal JsonObject? Entry() => Picked is not { } picked || !IsAnswered ? null : picked.Kind switch
    {
        ScopingChoiceKind.Option => new JsonObject { ["question"] = Question.Id, ["option"] = picked.OptionId, ["text"] = picked.Text },
        ScopingChoiceKind.Other => new JsonObject { ["question"] = Question.Id, ["other"] = OtherText.Trim() },
        ScopingChoiceKind.Skip => new JsonObject { ["question"] = Question.Id, ["skip"] = true },
        _ => null,
    };
}
