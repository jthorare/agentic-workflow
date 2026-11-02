using Aw.Judge.Core.Ids;
using Aw.Judge.Features.SetLabel;

namespace Aw.Judge.Testing;

/// <summary>Builds a SetLabelRequest with deterministic defaults: item 'item-d1', label 'continue'.</summary>
public sealed class SetLabelRequestBuilder
{
    private EvalItemId _item = EvalItemId.From("item-d1");
    private string _label = "continue";

    /// <summary>Sets the item.</summary>
    /// <param name="item">The eval item id.</param>
    /// <returns>This builder.</returns>
    public SetLabelRequestBuilder Item(string item)
    {
        _item = EvalItemId.From(item);
        return this;
    }

    /// <summary>Sets the label.</summary>
    /// <param name="label">The label, or 'skip'.</param>
    /// <returns>This builder.</returns>
    public SetLabelRequestBuilder Label(string label)
    {
        _label = label;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SetLabelRequest Build() => new(_item, _label);
}
