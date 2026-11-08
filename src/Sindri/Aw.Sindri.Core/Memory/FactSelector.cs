namespace Aw.Sindri.Core.Memory;

/// <summary>Which facts of a memory to forget: one by key, every one with a tag, or every expired one.</summary>
/// <param name="Key">The fact's key, or null.</param>
/// <param name="Tag">A tag, or null.</param>
/// <param name="ExpiredAt">Forget facts that expired at or before this time, or null.</param>
public sealed record FactSelector(string? Key, string? Tag, DateTimeOffset? ExpiredAt)
{
    /// <summary>Selects one fact.</summary>
    /// <param name="key">The fact's key.</param>
    /// <returns>The selector.</returns>
    public static FactSelector ByKey(string key) => new(Key: key, Tag: null, ExpiredAt: null);

    /// <summary>Selects every fact with a tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The selector.</returns>
    public static FactSelector ByTag(string tag) => new(Key: null, Tag: tag, ExpiredAt: null);

    /// <summary>Selects every expired fact.</summary>
    /// <param name="now">The time to compare ExpiresAt with.</param>
    /// <returns>The selector.</returns>
    public static FactSelector Expired(DateTimeOffset now) => new(Key: null, Tag: null, ExpiredAt: now);
}
