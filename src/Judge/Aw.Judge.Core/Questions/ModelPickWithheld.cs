namespace Aw.Judge.Core.Questions;

/// <summary>How many files the pack filters dropped, by reason: counts only, because a file name is itself information.</summary>
/// <param name="Secret">Files with a secret file name or a secret in their text.</param>
/// <param name="Denied">Files a denyPaths glob matched.</param>
public sealed record ModelPickWithheld(int Secret, int Denied);
