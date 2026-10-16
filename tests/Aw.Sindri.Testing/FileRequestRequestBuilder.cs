using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Features.FileRequest;

namespace Aw.Sindri.Testing;

/// <summary>Builds a FileRequestRequest with deterministic defaults: a re-scope request about no item.</summary>
public sealed class FileRequestRequestBuilder
{
    private RequestKind _kind = RequestKind.Rescope;
    private ItemRef? _item;
    private string _text = "Split the work into two items.";

    /// <summary>Sets the kind.</summary>
    /// <param name="value">The kind.</param>
    /// <returns>This builder.</returns>
    public FileRequestRequestBuilder OfKind(RequestKind value)
    {
        _kind = value;
        return this;
    }

    /// <summary>Sets the item.</summary>
    /// <param name="value">The id or alias, or null.</param>
    /// <returns>This builder.</returns>
    public FileRequestRequestBuilder AboutItem(ItemRef? value)
    {
        _item = value;
        return this;
    }

    /// <summary>Sets the text.</summary>
    /// <param name="value">The text.</param>
    /// <returns>This builder.</returns>
    public FileRequestRequestBuilder WithText(string value)
    {
        _text = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public FileRequestRequest Build() => new(_kind, _text, _item);
}
