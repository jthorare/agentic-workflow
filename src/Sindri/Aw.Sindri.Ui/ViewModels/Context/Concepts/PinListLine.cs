namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One concept of a pasted Add several list, as <see cref="PinListParser"/> split it.</summary>
/// <param name="Number">The concept's 1-based place in the list, as the rows name it ("line 3").</param>
/// <param name="Text">The line or block as pasted, its lines joined with a space, kept for a row that can't be read.</param>
/// <param name="Name">The concept's name; empty when the line has none.</param>
/// <param name="Definition">The words that define it, or null.</param>
/// <param name="Words">The words of a trailing "(also: word, word)", in order, without repeats.</param>
#pragma warning disable MA0182 // Q7b: the Add several view model and its tests use it
internal sealed record PinListLine(int Number, string Text, string Name, string? Definition, IReadOnlyList<string> Words)
{
    /// <summary>Gets a value indicating whether the line has a name of 1 to 80 characters; otherwise it is "Couldn't read this line" and never written.</summary>
    internal bool Readable => Name.Length is > 0 and <= PinListParser.MaxName;
}
#pragma warning restore MA0182
