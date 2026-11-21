namespace Aw.Meta.Core.Watch;

/// <summary>Tells a transient network failure of a gh or git call from any other error, by the text gh and git print.</summary>
public static class TransientGhError
{
    /// <summary>How many times a poll is retried after a transient error before it fails.</summary>
    public const int Retries = 3;

    /// <summary>Whether an error message names a transient network failure.</summary>
    /// <param name="message">The error message (gh's or git's first stderr line).</param>
    /// <returns>True when the call is worth trying again.</returns>
    public static bool IsTransient(string message) => throw new NotSupportedException();

    /// <summary>The wait before the retry number <paramref name="attempt"/> (1 is the first retry).</summary>
    /// <param name="attempt">The retry number.</param>
    /// <returns>The backoff.</returns>
    public static TimeSpan Backoff(int attempt) => throw new NotSupportedException();
}
