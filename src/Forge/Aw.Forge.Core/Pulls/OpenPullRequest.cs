using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>Opens a pull request from a branch the caller already pushed.</summary>
/// <param name="Repo">The repository.</param>
/// <param name="Head">The head branch.</param>
/// <param name="Base">The base branch.</param>
/// <param name="HeadSha">The pushed commit.</param>
/// <param name="Title">The title (redacted by the caller).</param>
/// <param name="Body">The body (redacted by the caller).</param>
/// <param name="Labels">Labels to put on the new pull request, or null for none. A forge that cannot apply them still opens the pull request; the snapshot's <see cref="PullSnapshot.Labels"/> shows what landed.</param>
/// <param name="Draft">Whether to open it as a draft.</param>
public sealed record OpenPullRequest(ForgeRepo Repo, string Head, string Base, string HeadSha, string Title, string Body, bool Draft, IReadOnlyList<string>? Labels = null);
