namespace Aw.Sindri.Core.Launches;

/// <summary>The tokens one model used, as a usage reader counts them.</summary>
/// <param name="Model">The model id the transcript names.</param>
/// <param name="Input">Fresh input tokens.</param>
/// <param name="CacheRead">Input tokens served from the cache.</param>
/// <param name="CacheWrite">Input tokens written to the cache.</param>
/// <param name="Output">Output tokens.</param>
public sealed record ModelTokens(string Model, long Input, long CacheRead, long CacheWrite, long Output)
{
    /// <summary>All four counts.</summary>
    public long Total => Input + CacheRead + CacheWrite + Output;
}
