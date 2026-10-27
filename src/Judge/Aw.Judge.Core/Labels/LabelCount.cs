namespace Aw.Judge.Core.Labels;

/// <summary>How many of one question's items are labelled.</summary>
/// <param name="Question">The question.</param>
/// <param name="Items">How many items it has.</param>
/// <param name="Labeled">How many carry a label other than 'skip'.</param>
/// <param name="Skipped">How many carry only 'skip' labels.</param>
public sealed record LabelCount(string Question, int Items, int Labeled, int Skipped);
