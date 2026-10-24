namespace Aw.Judge.Core.Traits;

/// <summary>One or more hunks of one file, sized for one Jev request.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="StartLine">The first new-file line.</param>
/// <param name="EndLine">The last new-file line.</param>
/// <param name="Text">The hunks' text, markers kept so the question can ask about the added lines.</param>
public sealed record DiffSource(string Path, int StartLine, int EndLine, string Text);
