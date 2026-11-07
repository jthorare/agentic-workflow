namespace Aw.Sindri.Core.Reviews;

/// <summary>A PR's head.</summary>
/// <param name="Sha">The head commit.</param>
/// <param name="Sha7">Its first 7 characters.</param>
/// <param name="Branch">The head branch (forge text).</param>
public sealed record ReviewPageHead(
    string Sha,
    string Sha7,
    string Branch);
