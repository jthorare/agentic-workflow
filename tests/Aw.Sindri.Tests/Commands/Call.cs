namespace Aw.Sindri.Tests.Commands;

/// <summary>One request a <see cref="RoutedApi"/> saw.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The absolute path.</param>
/// <param name="Query">The query string.</param>
/// <param name="Body">The body, or null.</param>
/// <param name="Headers">The headers.</param>
internal sealed record Call(string Method, string Path, string Query, string? Body, Dictionary<string, string> Headers);
