namespace Aw.Connectors.Testing.GitHub;

/// <summary>One request the fake server received.</summary>
/// <param name="Method">The method.</param>
/// <param name="Uri">The absolute url.</param>
/// <param name="Authorization">The Authorization header as sent, or null.</param>
/// <param name="UserAgent">The User-Agent header as sent.</param>
public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string UserAgent);
