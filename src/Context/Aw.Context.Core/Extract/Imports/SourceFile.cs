namespace Aw.Context.Core.Extract.Imports;

/// <summary>A file the import readers see: its repo-relative path and its text.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Text">The file's text.</param>
internal sealed record SourceFile(string Path, string Text);
