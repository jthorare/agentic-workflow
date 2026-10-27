namespace Aw.Sindri.Core.Events;

/// <summary>A file left out of a model pick's brief.</summary>
/// <param name="Path">The repo path.</param>
/// <param name="Chars">Its size in characters.</param>
/// <param name="Why">'cap', 'denied' or 'secret'; see <see cref="PickOmitReasons"/>.</param>
public sealed record OmittedFile(string Path, int Chars, string Why);
