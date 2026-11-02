namespace Aw.Judge.Core.Labels;

/// <summary>How often the outcome and the adjudicator labels agree on the items that have both.</summary>
/// <param name="Shared">Items carrying an outcome and an adjudicator label, neither 'skip'.</param>
/// <param name="Agreed">Of those, how many carry the same label.</param>
public sealed record LabelAgreement(int Shared, int Agreed);
