namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>An answer, read up to the 1 MB cap.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Body">The body as text.</param>
public sealed record GraphHttpResponse(int Status, string Body);
