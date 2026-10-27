namespace Aw.Judge.Core.Questions;

/// <summary>A file the pack left out.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="Chars">Its size in characters.</param>
/// <param name="Why">cap (over the size cap), denied (a denyPaths glob) or secret (a secret file name or a secret in its text).</param>
public sealed record ModelPickOmitted(string Path, int Chars, string Why);
