namespace Aw.Judge.Core.Traits;

/// <summary>One hunk of a file's diff: the new-file lines it spans and its text with the +/-/space markers.</summary>
/// <param name="Start">The first new-file line.</param>
/// <param name="End">The last new-file line.</param>
/// <param name="Text">The hunk header and its lines.</param>
public sealed record DiffHunk(int Start, int End, string Text);
