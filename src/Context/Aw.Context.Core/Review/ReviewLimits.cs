using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>The most entries a review listing returns: per group for the queue, in all for the history.</summary>
public static class ReviewLimits
{
    /// <summary>The queue's entries per group when a call names no limit.</summary>
    public const int QueueDefault = 100;

    /// <summary>The most a call may ask for.</summary>
    public const int Max = ReviewHistory.MaxLimit;

    /// <summary>The limit a call asks for.</summary>
    /// <param name="limit">The limit asked for, or null for <paramref name="fallback"/>.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The limit, or SND-CONTEXT-006 when it is outside 1 to 10000.</returns>
    public static Result<int> Resolve(int? limit, int fallback) =>
        limit is null ? fallback
        : limit is >= 1 and <= Max ? limit.Value
        : ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture, $"limit is 1 to {Max} (fix: --limit {fallback})"));
}
