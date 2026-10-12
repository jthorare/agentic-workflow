using Aw.Judge.Features.MapBriefByName;

namespace Aw.Judge.Testing;

/// <summary>Builds a MapBriefByNameRequest with deterministic defaults: the name 'helper'.</summary>
public sealed class MapBriefByNameRequestBuilder
{
    private string _name = "helper";

    /// <summary>Sets the teammate name.</summary>
    /// <param name="value">The name.</param>
    /// <returns>This builder.</returns>
    public MapBriefByNameRequestBuilder Named(string value)
    {
        _name = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public MapBriefByNameRequest Build() => new(_name);
}
