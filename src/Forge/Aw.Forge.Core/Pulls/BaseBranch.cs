namespace Aw.Forge.Core.Pulls;

/// <summary>The branch a pull request merges into.</summary>
/// <param name="Ref">The branch name.</param>
/// <param name="Sha">Its tip.</param>
/// <param name="IsDefault">Whether it is the repository's default branch.</param>
public sealed record BaseBranch(string Ref, string Sha, bool IsDefault);
