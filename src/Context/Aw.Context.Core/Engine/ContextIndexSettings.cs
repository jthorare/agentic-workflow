namespace Aw.Context.Core.Engine;

/// <summary>'context.index', checked, with its defaults (owned index spec §5.5, §5.6, §6, §8, Q7).</summary>
public sealed class ContextIndexSettings
{
    /// <summary>The default 'context.index.windowDays' (D12): about six months.</summary>
    public const int DefaultWindowDays = 183;

    /// <summary>The default 'context.index.maxChunks': a safety ceiling against a misconfigured window, never the bound.</summary>
    public const int DefaultMaxChunks = 60_000;

    /// <summary>How far back a first run reads; a record last updated before today minus this is never fetched, chunked or embedded.</summary>
    public int WindowDays { get; init; } = DefaultWindowDays;

    /// <summary>The most chunks the index holds before the embed job stops and says so.</summary>
    public int MaxChunks { get; init; } = DefaultMaxChunks;

    /// <summary>Records older than this many days are tombstoned by the daily reconcile; null (the default) is no limit.</summary>
    public int? RetainDays { get; init; }

    /// <summary>The dense leg's relevance floor.</summary>
    public DenseFloors Floors { get; init; } = new();

    /// <summary>True once the owner has accepted that Onyx's old volumes still hold raw Slack and Linear text (the 'index:onyx-leftover' health row).</summary>
    public bool AcknowledgeOnyxLeftover { get; init; }
}
