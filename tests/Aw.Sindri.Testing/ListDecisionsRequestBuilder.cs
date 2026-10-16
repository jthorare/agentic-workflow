using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ListDecisions;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ListDecisionsRequest with deterministic defaults: no filter.</summary>
public sealed class ListDecisionsRequestBuilder
{
    private bool? _pending;
    private ItemRef? _item;

    /// <summary>Sets the pending filter.</summary>
    /// <param name="value">True, false or null.</param>
    /// <returns>This builder.</returns>
    public ListDecisionsRequestBuilder Pending(bool? value)
    {
        _pending = value;
        return this;
    }

    /// <summary>Sets the item filter.</summary>
    /// <param name="value">The item, or null.</param>
    /// <returns>This builder.</returns>
    public ListDecisionsRequestBuilder AboutItem(ItemRef? value)
    {
        _item = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListDecisionsRequest Build() => new(_pending, _item);
}
