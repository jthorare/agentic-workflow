using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>The one due-word parser (todo spec D7): the quick-add box and the CLI read a trailing word, with the machine's local date passed in.</summary>
public static class DueWords
{
    /// <summary>Takes a trailing due word off <paramref name="typed"/>: 'today', 'tomorrow', 'mon'..'sun' (the next one, never today), 'MM-dd' (the next one, so next year once passed) or 'yyyy-MM-dd'. A trailing quote mark keeps the word as text.</summary>
    /// <param name="typed">The typed text.</param>
    /// <param name="today">The local date today.</param>
    /// <returns>The text and the date, or SND-TODO-003 for a word that looks like a date and isn't one.</returns>
    public static Result<DueSplit> Split(string typed, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(typed);
        _ = today;
        throw new NotSupportedException("DueWords.Split: make DueWordsTests pass");
    }

    /// <summary>Reads one due word, as '--due' takes it.</summary>
    /// <param name="word">The word.</param>
    /// <param name="today">The local date today.</param>
    /// <returns>The date, or SND-TODO-003.</returns>
    public static Result<DateOnly> Resolve(string word, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(word);
        _ = today;
        throw new NotSupportedException("DueWords.Resolve: make DueWordsTests pass");
    }
}
