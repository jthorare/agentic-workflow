namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One request of an 'http' node.</summary>
/// <param name="Method">GET, POST, PUT, PATCH or DELETE.</param>
/// <param name="Url">The absolute url.</param>
/// <param name="Headers">The headers; none that carry a credential.</param>
/// <param name="Body">The JSON body text, or null.</param>
public sealed record GraphHttpRequest(string Method, Uri Url, IReadOnlyDictionary<string, string> Headers, string? Body);
