namespace Aw.Sindri.Testing;

/// <summary>One request <see cref="FakeProvider"/> received.</summary>
/// <param name="Uri">The full URL.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Authorization">The Authorization header as sent, or null.</param>
/// <param name="Body">The request body, or an empty string.</param>
public sealed record ProviderRequest(Uri Uri, string Method, string? Authorization, string Body);
