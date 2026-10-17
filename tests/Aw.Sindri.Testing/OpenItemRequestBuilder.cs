using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.OpenItem;

namespace Aw.Sindri.Testing;

/// <summary>Builds an OpenItemRequest with deterministic defaults: the first item, a plain open (no target picked), at version 1.</summary>
public sealed class OpenItemRequestBuilder
{
    private ItemRef _item = ItemRef.To(A.FirstItemId);
    private LaunchMode _mode = LaunchMode.Open;
    private AgentId? _target;
    private long _version = 1;

    /// <summary>Sets the item.</summary>
    /// <param name="value">The id or alias.</param>
    /// <returns>This builder.</returns>
    public OpenItemRequestBuilder WithItem(ItemRef value)
    {
        _item = value;
        return this;
    }

    /// <summary>Sets the mode.</summary>
    /// <param name="value">Open, attach or resume.</param>
    /// <returns>This builder.</returns>
    public OpenItemRequestBuilder InMode(LaunchMode value)
    {
        _mode = value;
        return this;
    }

    /// <summary>Sets the agent the person picked.</summary>
    /// <param name="value">The agent, or null for the remembered one.</param>
    /// <returns>This builder.</returns>
    public OpenItemRequestBuilder WithTarget(AgentId? value)
    {
        _target = value;
        return this;
    }

    /// <summary>Sets the version the human saw.</summary>
    /// <param name="value">The version.</param>
    /// <returns>This builder.</returns>
    public OpenItemRequestBuilder WithVersion(long value)
    {
        _version = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public OpenItemRequest Build() => new(_item, _mode, _target, _version);
}
