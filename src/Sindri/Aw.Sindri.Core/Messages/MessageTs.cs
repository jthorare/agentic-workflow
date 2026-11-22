using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Messages;

/// <summary>A Slack message timestamp ('1700000000.000100'): a message's key inside its conversation and its place in time (messages spec §8.1).</summary>
/// <param name="Value">The text Slack sends.</param>
[JsonConverter(typeof(MessageTsJsonConverter))]
public readonly record struct MessageTs(string Value)
{
    private const int FractionDigits = 6;
    private const int MaxSecondsDigits = 15;

    /// <summary>Reads a Slack ts: seconds, a dot and six digits; any other text is refused.</summary>
    /// <param name="text">The text Slack sent.</param>
    /// <returns>The ts.</returns>
    /// <exception cref="FormatException">The text is not a Slack ts.</exception>
    public static MessageTs Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // the text is not echoed, so a hostile value never reaches a log line through the exception
        return TryParse(text, out var ts) ? ts : throw new FormatException("A Slack ts is seconds, a dot and six digits.");
    }

    /// <summary>Reads a Slack ts without throwing.</summary>
    /// <param name="text">The text Slack sent.</param>
    /// <param name="ts">The ts, or default when the text is not one.</param>
    /// <returns>True when the text is a Slack ts.</returns>
    public static bool TryParse(string? text, out MessageTs ts)
    {
        ts = IsWellFormed(text, out _) ? new MessageTs(text!) : default; // why: IsWellFormed is false for null
        return ts.Value is not null;
    }

    /// <summary>Orders two ts by time: by the seconds as a number, then the microseconds. A malformed ts (or default) sorts before every well-formed one.</summary>
    /// <param name="left">One ts.</param>
    /// <param name="right">The other.</param>
    /// <returns>Negative when <paramref name="left"/> is older, zero when equal, positive when newer.</returns>
    public static int Compare(MessageTs left, MessageTs right)
    {
        var leftOk = IsWellFormed(left.Value, out var leftDot);
        var rightOk = IsWellFormed(right.Value, out var rightDot);
        if (leftOk != rightOk)
        {
            return leftOk ? 1 : -1;
        }

        return leftOk
            ? CompareWellFormed(left.Value.AsSpan(), leftDot, right.Value.AsSpan(), rightDot)
            : string.CompareOrdinal(left.Value, right.Value);
    }

    private static int CompareWellFormed(ReadOnlySpan<char> left, int leftDot, ReadOnlySpan<char> right, int rightDot)
    {
        // digits only, so the longer seconds part (without leading zeros) is the bigger number; equal lengths compare as text
        var leftSeconds = left[..leftDot].TrimStart('0');
        var rightSeconds = right[..rightDot].TrimStart('0');
        var byLength = leftSeconds.Length.CompareTo(rightSeconds.Length);
        if (byLength != 0)
        {
            return byLength;
        }

        var bySeconds = leftSeconds.SequenceCompareTo(rightSeconds);
        return bySeconds != 0 ? bySeconds : left[(leftDot + 1)..].SequenceCompareTo(right[(rightDot + 1)..]);
    }

    private static bool IsWellFormed(string? text, out int dot)
    {
        dot = text?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        if (text is null || dot is < 1 or > MaxSecondsDigits || text.Length - dot - 1 != FractionDigits)
        {
            return false;
        }

        return AllDigits(text.AsSpan(0, dot)) && AllDigits(text.AsSpan(dot + 1));
    }

    private static bool AllDigits(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
