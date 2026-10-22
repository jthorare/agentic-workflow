namespace Aw.Sindri.Core.Checks;

/// <summary>One line the branch added.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Line">Its 1-based line number in the file at the head.</param>
/// <param name="Text">The line, without its line ending.</param>
public sealed record AddedLine(string Path, int Line, string Text);
