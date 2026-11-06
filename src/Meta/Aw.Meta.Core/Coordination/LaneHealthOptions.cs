namespace Aw.Meta.Core.Coordination;

/// <summary>How health judges the lanes.</summary>
/// <param name="UndeliveredAfter">How long a message may sit unseen by a lane whose hooks run before health flags it (AW_META_UNDELIVERED_MINUTES, default 5 minutes).</param>
public sealed record LaneHealthOptions(TimeSpan UndeliveredAfter)
{
    /// <summary>The defaults: five minutes.</summary>
    public static LaneHealthOptions Default { get; } = new(TimeSpan.FromMinutes(5));
}
