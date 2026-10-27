namespace Aw.Judge.Core.Questions;

/// <summary>One file (or written fact) of a model-pick call's pack.</summary>
/// <param name="Path">The file's repo path, or the fact's name.</param>
/// <param name="Text">Its text.</param>
public sealed record ModelPickFile(string Path, string Text);
