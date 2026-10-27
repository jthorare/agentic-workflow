namespace Aw.Judge.Core.Questions;

/// <summary>A single file cut at a line to fit the cap.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="Kept">How many characters stayed.</param>
/// <param name="Of">How many it had.</param>
public sealed record ModelPickCut(string Path, int Kept, int Of);
