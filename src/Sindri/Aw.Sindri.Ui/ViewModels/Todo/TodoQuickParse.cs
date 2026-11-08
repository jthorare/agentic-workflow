namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The quick-add box's due-word rule (todo spec §3.2, D7): the last word of the text is a day when it is today, tomorrow, a weekday
/// (full or short, meaning the next one, never today), MM-DD (the next such date, so next year once it has passed) or yyyy-MM-dd.
/// A closing quote mark keeps the day word as text: it is dropped and the word stays, with no date.
/// </summary>
internal static class TodoQuickParse
{
    /// <summary>Reads the text.</summary>
    /// <param name="input">What was typed.</param>
    /// <param name="today">Today in the machine's time zone.</param>
    /// <returns>The text to save and the day it read, if any. A day word with nothing before it stays text.</returns>
    internal static QuickParsed Parse(string input, DateOnly today)
    {
        var text = (input ?? string.Empty).Trim();
        var quoted = text.Length > 1 && text[^1] == '"';
        var body = quoted ? text[..^1].TrimEnd() : text;
        var cut = LastSpace(body);
        if (cut <= 0)
        {
            return new QuickParsed(text, Due: null);   // one word, or none: nothing is left to save if it were a day
        }

        var head = body[..cut].TrimEnd();
        if (head.Length == 0 || Day(body[(cut + 1)..], today) is not { } due)
        {
            return new QuickParsed(text, Due: null);
        }

        return quoted ? new QuickParsed(body, Due: null) : new QuickParsed(head, due);
    }

    private static DayOfWeek? Weekday(string word) => word switch
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

    private static int LastSpace(string text)
    {
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static DateOnly? Day(string word, DateOnly today)
    {
        var lower = word.ToLowerInvariant();
        if (lower == "today")
        {
            return today;
        }

        if (lower == "tomorrow")
        {
            return today.AddDays(1);
        }

        if (Weekday(lower) is { } weekday)
        {
            var ahead = ((int)weekday - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(ahead == 0 ? 7 : ahead);
        }

        return DateOnly.TryParseExact(lower, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var iso)
            ? iso
            : MonthDay(lower, today);
    }

    // "11-10": the next 10 November on or after today (a date that has passed this year is next year's; 02-29 waits for a leap year).
    private static DateOnly? MonthDay(string word, DateOnly today)
    {
        var parts = word.Split('-');
        if (parts.Length != 2 || !Digits(parts[0]) || !Digits(parts[1]))
        {
            return null;
        }

        var (month, day) = (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        if (month is < 1 or > 12 || day < 1)
        {
            return null;
        }

        for (var year = today.Year; year <= today.Year + 8; year++)
        {
            if (day <= DateTime.DaysInMonth(year, month) && new DateOnly(year, month, day) is var date && date >= today)
            {
                return date;
            }
        }

        return null;
    }

    private static bool Digits(string part) => part.Length is 1 or 2 && part.All(char.IsAsciiDigit);
}
