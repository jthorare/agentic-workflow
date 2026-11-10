using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>A length of time written as a block config says it: '90s', '15m', '2h', '1d' or a run of them such as '1h30m'.</summary>
public static class BlockDuration
{
    private static readonly TimeSpan _longest = TimeSpan.FromDays(3650);

    /// <summary>Reads a duration.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The duration, longer than zero and at most 3650 days, or the reason the text is not one.</returns>
    public static Result<TimeSpan> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rest = text.Trim();
        var total = TimeSpan.Zero;
        while (rest.Length > 0)
        {
            if (Part(rest) is not { } part)
            {
                return Bad(text);
            }

            total += part.Span;
            if (total > _longest)   // why: a sum past a few years overflows TimeSpan, or 'now + length' overflows DateTimeOffset, and either would throw
            {
                return Bad(text);
            }

            rest = rest[part.Length..];
        }

        return total > TimeSpan.Zero ? total : Bad(text);
    }

    // The number and unit the text starts with ('90s') and how many characters they take; null when it starts with anything else.
    private static (TimeSpan Span, int Length)? Part(string rest)
    {
        var digits = 0;
        while (digits < rest.Length && char.IsAsciiDigit(rest[digits]))
        {
            digits++;
        }

        if (digits == 0 || digits == rest.Length || digits > 6)
        {
            return null;
        }

        var count = int.Parse(rest[..digits], System.Globalization.CultureInfo.InvariantCulture);
        return Unit(rest[digits], count) is { } span ? (span, digits + 1) : null;
    }

    private static TimeSpan? Unit(char unit, int count) => unit switch
    {
        's' => TimeSpan.FromSeconds(count),
        'm' => TimeSpan.FromMinutes(count),
        'h' => TimeSpan.FromHours(count),
        'd' => TimeSpan.FromDays(count),
        _ => null,
    };

    private static Error Bad(string text) =>
        SindriErrors.GraphBlockFailed.With($"'{text}' is not a duration longer than zero; write it as 90s, 15m, 2h, 1d or 1h30m");
}
