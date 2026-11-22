namespace Aw.Transcripts.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A ListSessionsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ListSessionsRequestBuilder ListSessionsRequest() => new();
}
