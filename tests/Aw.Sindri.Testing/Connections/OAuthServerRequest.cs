namespace Aw.Sindri.Testing.Connections;

/// <summary>One request the fake OAuth server saw.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path.</param>
/// <param name="Form">The query and form fields together.</param>
public sealed record OAuthServerRequest(string Method, string Path, IReadOnlyDictionary<string, string> Form);
