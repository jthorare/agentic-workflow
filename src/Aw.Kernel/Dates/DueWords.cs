using System.Globalization;

namespace Aw.Kernel.Dates;

/// <summary>The one due-word parser (todo spec D7): the quick-add box and the CLI read a trailing word, with the machine's local date passed in.</summary>
public static class DueWords
{
    private const string Hint = "Use today, tomorrow, mon..sun, next fri, Dec 5, MM-dd or yyyy-MM-dd.";

    /// <summary>Takes a trailing due word off <paramref name="typed"/>: 'today', 'tomorrow', a weekday, short or long ('mon', 'thurs', 'friday'; the next one, never today), 'MM-dd' (the next one, so next year once passed) or 'yyyy-MM-dd'. Only the last word is taken, so the multi-word forms of <see cref="Resolve"/> are not read here. A trailing quote mark keeps the word as text.</summary>
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

    /// <summary>Reads one due word or phrase, as '--due' and the project Target box take it: the forms of <see cref="Split"/>, plus 'next fri', and a month and day ('Dec 5', '5 Dec', 'December 5', 'dec 5 2026', 'Dec 5, 2027'; without a year, the next such date on or after today).</summary>
    /// <param name="word">The word or phrase.</param>
    /// <param name="today">The local date today.</param>
    /// <returns>The date, or SND-TODO-003.</returns>
    public static Result<DateOnly> Resolve(string word, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(word);
        var w = word.Trim().ToLowerInvariant();
        var read = Relative(w, today) ?? MonthDay(w, today) ?? Written(w) ?? Phrase(w, today);
        return read is { } date ? date : DueErrors.BadDue.With($"'{word}' isn't a date. {Hint}");
    }

    // Two or three words: "next fri", "dec 5", "5 dec", "dec 5 2026".
    private static DateOnly? Phrase(string w, DateOnly today)
    {
        var words = w.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 2 && string.Equals(words[0], "next", StringComparison.Ordinal))
        {
            return Weekday(words[1]) is { } day ? Ahead(day, today) : null;
        }

        return words.Length is 2 or 3 ? WrittenMonthDay(words, today) : null;
    }

    private static DateOnly? WrittenMonthDay(string[] words, DateOnly today)
    {
        var monthFirst = Month(words[0]) > 0;
        var month = Month(monthFirst ? words[0] : words[1]);
        var dayWord = monthFirst ? words[1] : words[0];
        if (month == 0 || !IsNumber(dayWord, 2) || (words.Length == 3 && !IsNumber(words[2], 4)))
        {
            return null;
        }

        var dayOfMonth = int.Parse(dayWord, NumberStyles.None, CultureInfo.InvariantCulture);
        return words.Length == 3 ? Dated(int.Parse(words[2], NumberStyles.None, CultureInfo.InvariantCulture), month, dayOfMonth) : NextOn(month, dayOfMonth, today);
    }

    private static DateOnly? Dated(int year, int month, int day) => year >= 1 && Exists(year, month, day) ? new DateOnly(year, month, day) : null;

    private static DateOnly? NextOn(int month, int day, DateOnly today)
    {
        for (var year = today.Year; year <= today.Year + 8; year++)
        {
            if (Exists(year, month, day) && new DateOnly(year, month, day) >= today)
            {
                return new DateOnly(year, month, day);
            }
        }

        return null;
    }

    private static bool IsNumber(string s, int maxDigits) => s.Length > 0 && s.Length <= maxDigits && IsDigits(s, 0, s.Length) && (maxDigits != 4 || s.Length == 4);

    private static int Month(string w) => w switch
    {
        "jan" or "january" => 1,
        "feb" or "february" => 2,
        "mar" or "march" => 3,
        "apr" or "april" => 4,
        "may" => 5,
        "jun" or "june" => 6,
        "jul" or "july" => 7,
        "aug" or "august" => 8,
        "sep" or "sept" or "september" => 9,
        "oct" or "october" => 10,
        "nov" or "november" => 11,
        "dec" or "december" => 12,
        _ => 0,
    };

    private static DateOnly Ahead(DayOfWeek target, DateOnly today)
    {
        var ahead = ((int)target - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(ahead == 0 ? 7 : ahead);
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

        return Ahead(target, today);
    }

    private static DateOnly? MonthDay(string w, DateOnly today)
    {
        if (w.Length != 5 || w[2] != '-' || !IsDigits(w, 0, 2) || !IsDigits(w, 3, 2))
        {
            return null;
        }

        var month = Number(w, 0, 2);
        var day = Number(w, 3, 2);
        return NextOn(month, day, today);
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
        "mon" or "monday" => DayOfWeek.Monday,
        "tue" or "tues" or "tuesday" => DayOfWeek.Tuesday,
        "wed" or "weds" or "wednesday" => DayOfWeek.Wednesday,
        "thu" or "thur" or "thurs" or "thursday" => DayOfWeek.Thursday,
        "fri" or "friday" => DayOfWeek.Friday,
        "sat" or "saturday" => DayOfWeek.Saturday,
        "sun" or "sunday" => DayOfWeek.Sunday,
        _ => null,
    };
}
