namespace Aw.Context.Testing.Fakes;

/// <summary>Options for a fake server.</summary>
public sealed record FakeServerOptions
{
    /// <summary>The credential the server accepts; null takes the server's default fixture token.</summary>
    public string? Token { get; init; }

    /// <summary>How long the fake waits before it answers each request (to test a client's deadline); zero by default.</summary>
    public TimeSpan Delay { get; init; }

    /// <summary>Extra recorded-looking projects the fake Linear server adds (newer than the recordings), to page and to crowd a scope with; zero by default.</summary>
    public int LinearExtraProjects { get; init; }

    /// <summary>The team key the extra projects belong to ('ENG' by default).</summary>
    public string LinearExtraProjectsTeam { get; init; } = "ENG";

    /// <summary>How many comments the fake Linear server adds to issue SPL-105 on top of the recorded ones; zero by default.</summary>
    public int LinearExtraComments { get; init; }

    /// <summary>Whether the fake Linear server serves a connection newest first, as Linear does for orderBy updatedAt and createdAt (the default keeps the oldest first, which the paging tests of the fake itself rely on).</summary>
    public bool LinearNewestFirst { get; init; }

    /// <summary>How the fake Linear server rate limits every request: none (the default), HTTP 429, or a GraphQL RATELIMITED error.</summary>
    public LinearRateLimit LinearRateLimit { get; init; }
}
