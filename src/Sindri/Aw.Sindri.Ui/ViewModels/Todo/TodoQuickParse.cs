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
    /// <returns>The text to save and the day it read, if any.</returns>
    internal static QuickParsed Parse(string input, DateOnly today) => throw new NotSupportedException();
}
