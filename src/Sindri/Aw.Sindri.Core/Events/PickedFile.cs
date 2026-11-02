namespace Aw.Sindri.Core.Events;

/// <summary>A file Jev saw for a model pick.</summary>
/// <param name="Path">The repo path.</param>
/// <param name="Chars">How many characters of it were shown.</param>
public sealed record PickedFile(string Path, int Chars);
