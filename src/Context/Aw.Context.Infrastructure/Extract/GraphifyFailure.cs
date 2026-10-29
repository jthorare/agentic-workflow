using System.Globalization;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Words for a graphify run that ended with a non-zero exit: the signal when a shell reports one (128+N), and the scrubbed end of stderr, where a crash prints last (#1680).</summary>
internal static class GraphifyFailure
{
    /// <summary>The most stderr lines that reach a message.</summary>
    internal const int MaxLines = 5;

    /// <summary>The most stderr characters that reach a message.</summary>
    internal const int MaxCharacters = 500;

    /// <summary>"graphify exited with code X" or "graphify was killed by signal N (exit X)", then ": tail" when stderr has one.</summary>
    /// <param name="exit">The exit code.</param>
    /// <param name="stderr">What graphify printed to stderr (its end is enough).</param>
    /// <param name="scrub">Redacts secrets from the tail.</param>
    internal static string Message(int exit, string stderr, Func<string, string> scrub)
    {
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(scrub);
        var head = exit is >= 129 and <= 159
            ? string.Create(CultureInfo.InvariantCulture, $"graphify was killed by signal {exit - 128} (exit {exit})")
            : string.Create(CultureInfo.InvariantCulture, $"graphify exited with code {exit}");
        var tail = Tail(stderr, scrub);
        return tail.Length == 0 ? head : head + ": " + tail;
    }

    /// <summary>The last non-empty lines of stderr (at most <see cref="MaxLines"/>, <see cref="MaxCharacters"/> characters), scrubbed and joined on one line with " | ".</summary>
    /// <param name="stderr">The stderr text.</param>
    /// <param name="scrub">Redacts secrets.</param>
    internal static string Tail(string stderr, Func<string, string> scrub)
    {
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(scrub);
        var lines = stderr.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var joined = string.Join(" | ", lines.TakeLast(MaxLines));
        if (joined.Length > MaxCharacters)
        {
            joined = joined[^MaxCharacters..];
        }

        var scrubbed = scrub(joined);
        return scrubbed.Length > MaxCharacters ? scrubbed[^MaxCharacters..] : scrubbed;
    }
}
