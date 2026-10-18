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

    /// <summary>The input (fresh, cache read and cache write) and output totals over every model.</summary>
    /// <param name="tokens">The per-model counts.</param>
    /// <returns>Input and output tokens in all.</returns>
    public static (long Input, long Output) Totals(IReadOnlyList<ModelTokens> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return (tokens.Sum(static t => t.Input + t.CacheRead + t.CacheWrite), tokens.Sum(static t => t.Output));
    }

    /// <summary>The model with the most tokens, or null for none (ties break by name, so the answer is stable).</summary>
    /// <param name="tokens">The per-model counts.</param>
    /// <returns>The model id.</returns>
    public static string? Dominant(IReadOnlyList<ModelTokens> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return tokens.OrderByDescending(static t => t.Total).ThenBy(static t => t.Model, StringComparer.Ordinal).FirstOrDefault()?.Model;
    }
}
