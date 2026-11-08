using Aw.Kernel.Dates;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The quick-add box's due-word rule (todo spec §3.2, D7): it finds the last word of the text; <see cref="DueWords"/> (the CLI's table) says
/// whether that word is a day and which one.
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
            return new QuickParsed(quoted ? body : text, Due: null);   // one word, or none: nothing is left to save if it were a day
        }

        var head = body[..cut].TrimEnd();
        var read = DueWords.Resolve(body[(cut + 1)..], today);
        if (head.Length == 0 || read.IsError)
        {
            return new QuickParsed(text, Due: null);
        }

        var due = read.Value;

        return quoted ? new QuickParsed(body, Due: null) : new QuickParsed(head, due);
    }

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
}
