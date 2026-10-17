using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>Changes a pull request's title or body; null leaves a field alone.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="Title">The new title.</param>
/// <param name="Body">The new body.</param>
public sealed record UpdatePullRequest(ForgeRef Pull, string? Title, string? Body);
