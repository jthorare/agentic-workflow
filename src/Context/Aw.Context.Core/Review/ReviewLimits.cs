using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>The limit a review listing was asked for: per group for the queue, in all for the history. A call that names none gets everything.</summary>
public static class ReviewLimits
{
    /// <summary>The queue's entries per group when a call names no limit: all of them.</summary>
    public const int QueueDefault = int.MaxValue;

    /// <summary>The limit a call asks for.</summary>
    /// <param name="limit">The limit asked for, or null for <paramref name="fallback"/>.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The limit, or SND-CONTEXT-006 when it is under 1.</returns>
    public static Result<int> Resolve(int? limit, int fallback) =>
        limit is null ? fallback
        : limit is >= 1 ? limit.Value
        : ContextErrors.QueryInvalid.With("limit is 1 or more (fix: leave --limit out to read everything)");
}
