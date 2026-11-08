using System.Globalization;

namespace Aw.Kernel.Dates;

/// <summary>The one due-word parser (todo spec D7): the quick-add box and the CLI read a trailing word, with the machine's local date passed in.</summary>
public static class DueWords
{
    private const string Hint = "Use today, tomorrow, mon..sun, MM-dd or yyyy-MM-dd.";

    /// <summary>Takes a trailing due word off <paramref name="typed"/>: 'today', 'tomorrow', 'mon'..'sun' (the next one, never today), 'MM-dd' (the next one, so next year once passed) or 'yyyy-MM-dd'. A trailing quote mark keeps the word as text.</summary>
    /// <param name="typed">The typed text.</param>
    /// <param name="today">The local date today.</param>
    /// <returns>The text and the date, or SND-TODO-003 for a word that looks like a date and isn't one.</returns>
    public static Result<DueSplit> Split(string typed, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var text = typed.Trim();
        if (text.EndsWith('"'))
        {
            return new DueSplit(text[..^1].TrimEnd(), Due: null);
        }

        var cut = text.LastIndexOfAny([' ', '\t']);
        if (cut < 0)
        {
            return new DueSplit(text, Due: null);
        }

        var word = text[(cut + 1)..];
        var rest = text[..cut].TrimEnd();
        var read = Resolve(word, today);
        if (read.IsSuccess)
        {
            return new DueSplit(rest, read.Value);
        }

        return LooksLikeDate(word) ? read.Error : new DueSplit(text, Due: null);
    }

    /// <summary>Reads one due word, as '--due' takes it.</summary>
    /// <param name="word">The word.</param>
    /// <param name="today">The local date today.</param>
    /// <returns>The date, or SND-TODO-003.</returns>
    public static Result<DateOnly> Resolve(string word, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(word);
        var w = word.Trim().ToLowerInvariant();
        var read = Relative(w, today) ?? MonthDay(w, today) ?? Written(w);
        return read is { } date ? date : DueErrors.BadDue.With($"'{word}' isn't a date. {Hint}");
    }

    private static DateOnly? Relative(string w, DateOnly today)
    {
        if (string.Equals(w, "today", StringComparison.Ordinal))
        {
            return today;
        }

        if (string.Equals(w, "tomorrow", StringComparison.Ordinal))
        {
            return today.AddDays(1);
        }

        if (Weekday(w) is not { } target)
        {
            return null;
        }

        var ahead = ((int)target - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(ahead == 0 ? 7 : ahead);
    }

    private static DateOnly? MonthDay(string w, DateOnly today)
    {
        if (w.Length != 5 || w[2] != '-' || !IsDigits(w, 0, 2) || !IsDigits(w, 3, 2))
        {
            return null;
        }

        var month = Number(w, 0, 2);
        var day = Number(w, 3, 2);
        for (var year = today.Year; year <= today.Year + 8; year++)
        {
            if (Exists(year, month, day) && new DateOnly(year, month, day) >= today)
            {
                return new DateOnly(year, month, day);
            }
        }

        return null;
    }

    private static bool Exists(int year, int month, int day) => month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);

    private static DateOnly? Written(string w) =>
        w.Length == 10 && DateOnly.TryParseExact(w, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var written) ? written : null;

    private static bool LooksLikeDate(string word) =>
        (word.Length == 5 && IsDigits(word, 0, 2) && word[2] == '-' && IsDigits(word, 3, 2))
        || (word.Length == 10 && IsDigits(word, 0, 4) && word[4] == '-' && IsDigits(word, 5, 2) && word[7] == '-' && IsDigits(word, 8, 2));

    private static bool IsDigits(string s, int start, int count)
    {
        for (var i = start; i < start + count; i++)
        {
            if (i >= s.Length || s[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static int Number(string s, int start, int count) => int.Parse(s.AsSpan(start, count), NumberStyles.None, CultureInfo.InvariantCulture);

    private static DayOfWeek? Weekday(string w) => w switch
    {
        "mon" => DayOfWeek.Monday,
        "tue" => DayOfWeek.Tuesday,
        "wed" => DayOfWeek.Wednesday,
        "thu" => DayOfWeek.Thursday,
        "fri" => DayOfWeek.Friday,
        "sat" => DayOfWeek.Saturday,
        "sun" => DayOfWeek.Sunday,
        _ => null,
    };
}
