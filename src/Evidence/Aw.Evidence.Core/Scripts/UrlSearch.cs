using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>One query parameter of the page's URL has <paramref name="Value"/> (a URL-backed filter); the path is not checked.</summary>
/// <param name="Param">The query parameter's name (not empty).</param>
/// <param name="Value">The expected value (may be empty).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UrlSearch(string Param, string Value) : ExpectedState
{
    /// <inheritdoc/>
    public override string Describe() => $"url ?{Param}={Value}";

    /// <inheritdoc/>
    public override string? Problem() => Param.Length == 0 ? "url-search needs a param" : null;
}
