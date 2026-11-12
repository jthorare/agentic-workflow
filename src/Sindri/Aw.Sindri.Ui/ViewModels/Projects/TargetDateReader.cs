using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// Reads the forms people type in the Add project sheet's Target box ("Dec 5", "next fri", "2026-12-05", "tomorrow") into a date. Pure: today comes in as an argument,
/// so a relative form resolves from the caller's clock. A weekday means the first such day after today; a month and day without a year means the next such date on or
/// after today.
/// </summary>
internal static class TargetDateReader
{
    private static readonly string[] _months = ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];
    private static readonly string[] _days = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    /// <summary>Reads the text.</summary>
    /// <param name="text">What is in the box.</param>
    /// <param name="today">Today's date, from the caller's clock.</param>
    /// <returns>Empty for blank text, the date for a readable one, Unreadable otherwise.</returns>
    internal static TargetReading Read(string? text, DateOnly today)
    {
        var words = (text ?? string.Empty).ToLowerInvariant().Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return new TargetReading(TargetReadKind.Empty, default);
        }

        return Find(words, today) is { } date ? new TargetReading(TargetReadKind.Date, date) : new TargetReading(TargetReadKind.Unreadable, default);
    }

    private static DateOnly? Find(string[] words, DateOnly today)
    {
        if (words.Length == 1)
        {
            if (DateOnly.TryParseExact(words[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            {
                return iso;
            }

            if (string.Equals(words[0], "today", StringComparison.Ordinal))
            {
                return today;
            }

            if (string.Equals(words[0], "tomorrow", StringComparison.Ordinal))
            {
                return today.AddDays(1);
            }
        }

        var rest = string.Equals(words[0], "next", StringComparison.Ordinal) ? words[1..] : words;
        if (rest.Length == 1 && Index(_days, rest[0]) is >= 0 and var day)
        {
            var ahead = ((day - (int)today.DayOfWeek + 6) % 7) + 1;   // 1..7: today's own weekday means a week from today
            return today.AddDays(ahead);
        }

        return ReferenceEquals(rest, words) ? MonthDay(words, today) : null;
    }

    // "dec 5", "5 dec", each with a trailing year.
    private static DateOnly? MonthDay(string[] words, DateOnly today)
    {
        if (words.Length is < 2 or > 3)
        {
            return null;
        }

        var monthFirst = Index(_months, words[0]) >= 0;
        var (monthWord, dayWord) = monthFirst ? (words[0], words[1]) : (words[1], words[0]);
        var month = Index(_months, monthWord) + 1;
        if (month == 0 || !int.TryParse(dayWord, NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            return null;
        }

        if (words.Length == 3)
        {
            return words[2].Length == 4 && int.TryParse(words[2], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? Build(year, month, day) : null;
        }

        return NextOn(month, day, today);
    }

    // why: a leap day without a year may need the year after next, so look up to four years ahead
    private static DateOnly? NextOn(int month, int day, DateOnly today)
    {
        for (var year = today.Year; year <= today.Year + 4; year++)
        {
            if (Build(year, month, day) is { } date && date >= today)
            {
                return date;
            }
        }

        return null;
    }

    private static DateOnly? Build(int year, int month, int day) =>
        year is >= 1 and <= 9999 && day >= 1 && day <= DateTime.DaysInMonth(year, month) ? new DateOnly(year, month, day) : null;

    // The index of the name the word abbreviates (three letters or more: "fri", "thurs", "sept"), or -1.
    private static int Index(string[] names, string word) =>
        word.Length >= 3 ? Array.FindIndex(names, n => n.StartsWith(word, StringComparison.Ordinal)) : -1;
}
