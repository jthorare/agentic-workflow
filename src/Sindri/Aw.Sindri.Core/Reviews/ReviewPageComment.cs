namespace Aw.Sindri.Core.Reviews;

/// <summary>One comment.</summary>
/// <param name="Author">The author's login.</param>
/// <param name="Body">The text (forge text).</param>
/// <param name="At">When it was written.</param>
public sealed record ReviewPageComment(
    string Author,
    string Body,
    DateTimeOffset At);
