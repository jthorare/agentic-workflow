using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.SetLabel;

/// <summary>The SetLabel request ('judge label set &lt;itemId&gt; &lt;label|skip&gt;'): a manual override.</summary>
/// <param name="Item">The eval item.</param>
/// <param name="Label">One of the item's question's outputs, or 'skip'.</param>
public sealed record SetLabelRequest(EvalItemId Item, string Label);
