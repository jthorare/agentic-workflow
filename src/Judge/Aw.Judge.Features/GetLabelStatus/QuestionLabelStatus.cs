using Aw.Judge.Core.Labels;

namespace Aw.Judge.Features.GetLabelStatus;

/// <summary>One question's label coverage.</summary>
/// <param name="Question">The question.</param>
/// <param name="Items">How many eval items it has.</param>
/// <param name="Labeled">How many carry a label other than 'skip'.</param>
/// <param name="Skipped">How many carry only 'skip' labels.</param>
/// <param name="Agreement">How often its outcome and adjudicator labels agree.</param>
public sealed record QuestionLabelStatus(string Question, int Items, int Labeled, int Skipped, LabelAgreement Agreement);
