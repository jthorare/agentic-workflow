namespace Aw.Meta.Core.Watch;

/// <summary>Tells a transient network failure of a gh or git call from any other error, by the text gh and git print. Our own "timed out" (a call that ran past its limit) is not transient here: it already waited that long.</summary>
public static class TransientGhError
{
    /// <summary>How many times a poll is retried after a transient error before it fails.</summary>
    public const int Retries = 3;

    private static readonly string[] _markers =
    [
        "unexpected EOF",
        "early EOF",
        "TLS handshake timeout",
        "i/o timeout",
        "connection reset",
        "connection refused",
        "Connection timed out",
        "Could not resolve host",
        "no such host",
        "error connecting to",
        "network is unreachable",
        "HTTP 502",
        "HTTP 503",
        "HTTP 504",
        "Bad Gateway",
        "Service Unavailable",
        "Gateway Timeout",
    ];

    /// <summary>Whether an error message names a transient network failure.</summary>
    /// <param name="message">The error message (gh's or git's first stderr line).</param>
    /// <returns>True when the call is worth trying again.</returns>
    public static bool IsTransient(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _markers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The wait before the retry number <paramref name="attempt"/> (1 is the first retry): 5, 15, then 45 seconds.</summary>
    /// <param name="attempt">The retry number.</param>
    /// <returns>The backoff.</returns>
    public static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(5 * Math.Pow(3, Math.Clamp(attempt, 1, Retries) - 1));
}
