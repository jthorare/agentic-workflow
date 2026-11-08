using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>A todo's text rules (todo spec D3): one line of 1 to 280 characters, no Control or Format character, no line or paragraph separator, none of the note's markers and no markdown link.</summary>
public static class TodoText
{
    /// <summary>The longest text, in characters.</summary>
    public const int MaxLength = 280;

    /// <summary>Checks <paramref name="text"/>.</summary>
    /// <param name="text">The typed text.</param>
    /// <returns>The trimmed text, SND-TODO-001 for an empty or over-long one, or SND-TODO-007 for a forbidden character, marker or link.</returns>
    public static Result<string> Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        throw new NotSupportedException("TodoText.Check: make TodoTextTests pass");
    }
}
