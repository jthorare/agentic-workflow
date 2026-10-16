using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Features.ListRequests;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ListRequestsRequest with deterministic defaults: no filter.</summary>
public sealed class ListRequestsRequestBuilder
{
    private RequestState? _state;
    private ItemRef? _item;

    /// <summary>Sets the state filter.</summary>
    /// <param name="value">The state, or null.</param>
    /// <returns>This builder.</returns>
    public ListRequestsRequestBuilder InState(RequestState? value)
    {
        _state = value;
        return this;
    }

    /// <summary>Sets the item filter.</summary>
    /// <param name="value">The item, or null.</param>
    /// <returns>This builder.</returns>
    public ListRequestsRequestBuilder AboutItem(ItemRef? value)
    {
        _item = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListRequestsRequest Build() => new(_state, _item);
}
