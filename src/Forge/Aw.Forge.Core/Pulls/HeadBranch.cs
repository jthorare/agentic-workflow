namespace Aw.Forge.Core.Pulls;

/// <summary>The branch a pull request comes from.</summary>
/// <param name="Ref">The branch name.</param>
/// <param name="Sha">Its tip.</param>
/// <param name="Fork">Whether it lives in another repository.</param>
/// <param name="Repo">The repository the branch lives in as "owner/name", or null when the forge does not say.</param>
public sealed record HeadBranch(string Ref, string Sha, bool Fork, string? Repo = null);
