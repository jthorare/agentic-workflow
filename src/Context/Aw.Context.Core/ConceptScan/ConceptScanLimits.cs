namespace Aw.Context.Core.ConceptScan;

/// <summary>The scan's budget (owner, q110): two calls of at most 60,000 input characters and 4,000 output tokens, two minutes each, six scans a day.</summary>
public static class ConceptScanLimits
{
    /// <summary>Input characters per model call.</summary>
    public const int MaxInputChars = 60_000;

    /// <summary>Output tokens per model call.</summary>
    public const int MaxOutputTokens = 4_000;

    /// <summary>Scans a repo may start in a rolling 24 hours.</summary>
    public const int MaxScansPerDay = 6;

    /// <summary>Concepts one reply may hold; more are dropped, lowest confidence first.</summary>
    public const int MaxConcepts = 15;

    /// <summary>Searches the scan runs in all.</summary>
    public const int MaxSearches = 24;

    /// <summary>Hits one search returns.</summary>
    public const int HitsPerSearch = 6;

    /// <summary>How long one model call may take.</summary>
    public static TimeSpan CallTimeout { get; } = TimeSpan.FromMinutes(2);

    /// <summary>The span the daily count looks back over.</summary>
    public static TimeSpan Window { get; } = TimeSpan.FromHours(24);
}
