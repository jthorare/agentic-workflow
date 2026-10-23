using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One question of the ask step with its answer field: the question, where it came from, the answers it offers as choices, and what the person typed or chose.</summary>
internal sealed class DeskQuestionViewModel : ObservableObject
{
    private readonly Action _changed;

    internal DeskQuestionViewModel(DeskQuestion question, Action changed)
    {
        ArgumentNullException.ThrowIfNull(question);
        (Question, _changed) = (question, changed);
        Options = [.. question.Options.Select(o => new DeskQuestionOptionViewModel(this, o))];
        Context = Labels.NeedsYouQuestionContext(question.Lens);
    }

    /// <summary>The served question.</summary>
    internal DeskQuestion Question { get; }

    /// <summary>The question in words.</summary>
    public string Prompt => Question.Question;

    /// <summary>Where the question came from (the review and its finding); empty when the API names neither.</summary>
    public string Context { get; }

    /// <summary>True when there is a context line.</summary>
    public bool HasContext => Context.Length > 0;

    /// <summary>The answers the question offers; empty for a free answer.</summary>
    public IReadOnlyList<DeskQuestionOptionViewModel> Options { get; }

    /// <summary>True when the question offers answers.</summary>
    public bool HasOptions => Options.Count > 0;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The answer field's placeholder.</summary>
    public string AnswerPlaceholder => Labels.NeedsYouAnswerPlaceholder;
#pragma warning restore CA1822, MA0041

    /// <summary>The answer field's accessible name.</summary>
    public string AnswerName => Labels.NeedsYouAnswerName(Question.Question);

    /// <summary>The answer: typed, or filled by choosing an option.</summary>
    public string Answer
    {
        get;
        set
        {
            var next = value ?? string.Empty;
            if (!string.Equals(field, next, StringComparison.Ordinal))
            {
                field = next;
                OnChanged(nameof(Answer));
                OnChanged(nameof(IsAnswered));
                _changed();
            }
        }
    } = string.Empty;

    /// <summary>True when the answer has words.</summary>
    public bool IsAnswered => !string.IsNullOrWhiteSpace(Answer);
}
