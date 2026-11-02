namespace Aw.Judge.Core.Labels;

/// <summary>An eval item with the label that scores it.</summary>
/// <param name="Item">The item.</param>
/// <param name="Label">The label: one of the question's outputs ('skip' is never returned).</param>
/// <param name="Source">Who gave the label; when several sources labelled the item, the strongest (override, then outcome, then adjudicator).</param>
public sealed record LabeledItem(EvalItem Item, string Label, LabelSource Source);
