namespace Aw.Forge.Core.Pulls;

/// <summary>The branch a pull request comes from.</summary>
/// <param name="Ref">The branch name.</param>
/// <param name="Sha">Its tip.</param>
/// <param name="Fork">Whether it lives in another repository.</param>
public sealed record HeadBranch(string Ref, string Sha, bool Fork);
