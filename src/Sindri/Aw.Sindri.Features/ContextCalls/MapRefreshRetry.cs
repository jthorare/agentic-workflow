using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>When a map refresh that failed runs again (owner, 2026-10-24: a busy store or an unavailable model "should just be retried"): a transient error waits 30 s, then 2 min, then 10 min, for four attempts in all.</summary>
internal static class MapRefreshRetry
{
    /// <summary>How many times the job runs before its last error stands.</summary>
    internal const int Attempts = 4;

    private static readonly TimeSpan[] _waits = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

    /// <summary>
    /// Whether a retry can help: a dependency that was unavailable, a timeout or a rate limit. Invalid, not found, conflict, budget and forbidden errors are not retried, and neither is a wait for the map build slot
    /// that ran out (owner, 2026-11-05, #2106): the build ahead of it already held the slot for 30 min, so another 30 min of waiting would only hide it; the job fails as timed out.
    /// </summary>
    internal static bool Transient(Error error) =>
        error.Kind is ErrorKind.Unavailable or ErrorKind.Timeout or ErrorKind.RateLimited && error.Code != ContextErrors.MapBuildWaitTimedOut.Code;

    /// <summary>How long to wait before the given attempt (2 to 4), or null when the error is not transient or the attempts are spent.</summary>
    internal static TimeSpan? WaitBefore(int attempt, Error error) =>
        attempt is > 1 and <= Attempts && Transient(error) ? _waits[attempt - 2] : null;

    /// <summary>The progress line that says the job is waiting to retry.</summary>
    internal static string Line(int attempt, TimeSpan wait, Error error) =>
        string.Create(CultureInfo.InvariantCulture, $"retrying (attempt {attempt} of {Attempts}) in {Format(wait)} after {error.Code}: {error.Message}");

    private static string Format(TimeSpan wait) => wait.TotalMinutes >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{wait.TotalMinutes:0.#} min")
        : string.Create(CultureInfo.InvariantCulture, $"{wait.TotalSeconds:0.#} s");
}
