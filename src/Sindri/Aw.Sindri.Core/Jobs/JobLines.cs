using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The pure rules for a job's progress lines.</summary>
public static class JobLines
{
    /// <summary>How many leading characters a long failure summary keeps.</summary>
    public const int FailureHeadChars = 60;

    /// <summary>A progress line as it is recorded: scrubbed, ANSI and OSC sequences escaped, folded to one line and cut to <see cref="JobLimits.LineChars"/>.</summary>
    /// <param name="scrubber">Takes secrets out.</param>
    /// <param name="line">What the work reported.</param>
    /// <returns>The clean line.</returns>
    public static string Clean(ITextScrubber scrubber, string line)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentNullException.ThrowIfNull(line);
        return SafeText.Clean(scrubber, line, JobLimits.LineChars);
    }

    /// <summary>
    /// A failed job's summary: cleaned like <see cref="Clean"/> (the scrub runs over the whole text before any cut), but a line over
    /// <see cref="JobLimits.LineChars"/> keeps its head and its tail joined by '…', because the cause of an error (a SQLite reason,
    /// an exit code) comes last. The head gets <see cref="FailureHeadChars"/> characters and the tail the rest.
    /// </summary>
    /// <param name="scrubber">Takes secrets out.</param>
    /// <param name="message">The error's message.</param>
    /// <returns>The clean summary, at most <see cref="JobLimits.LineChars"/> characters.</returns>
    public static string CleanFailure(ITextScrubber scrubber, string message)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentNullException.ThrowIfNull(message);
        var line = SafeText.Clean(scrubber, message, int.MaxValue);
        if (line.Length <= JobLimits.LineChars)
        {
            return line;
        }

        var head = FailureHeadChars;
        if (char.IsHighSurrogate(line[head - 1]))
        {
            head--;
        }

        var tailStart = line.Length - (JobLimits.LineChars - FailureHeadChars - 1);
        if (char.IsLowSurrogate(line[tailStart]))
        {
            tailStart++;
        }

        return line[..head] + "…" + line[tailStart..];
    }

    /// <summary>The lines after adding <paramref name="next"/>: the last <see cref="JobLimits.ViewLines"/>.</summary>
    /// <param name="lines">The lines so far.</param>
    /// <param name="next">The new line.</param>
    /// <returns>The kept lines, oldest first.</returns>
    public static IReadOnlyList<JobLine> Keep(IReadOnlyList<JobLine> lines, JobLine next)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(next);
        var skip = Math.Max(0, lines.Count + 1 - JobLimits.ViewLines);
        return [.. lines.Skip(skip), next];
    }
}
