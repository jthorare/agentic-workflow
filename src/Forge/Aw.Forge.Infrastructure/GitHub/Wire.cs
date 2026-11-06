namespace Aw.Forge.Infrastructure.GitHub;

/// <summary>One request on the wire.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Url">The absolute URL.</param>
/// <param name="Body">The JSON body, or null.</param>
/// <param name="Raw">Whether to ask for GitHub's raw media type.</param>
internal sealed record Wire(HttpMethod Method, Uri Url, byte[]? Body, bool Raw = false);
