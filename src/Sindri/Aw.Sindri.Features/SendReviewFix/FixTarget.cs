using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>The PR a fix task is for, once its repo, record and head are known.</summary>
/// <param name="Slug">The PR's repository as "owner/name".</param>
/// <param name="Link">The PR's link.</param>
/// <param name="HeadRepo">The head repository as "owner/name".</param>
/// <param name="Branch">The head branch.</param>
/// <param name="PostsThread">Whether the PR is the owner's or an agent's, so the finding is posted as a thread.</param>
/// <param name="Repo">The profile repo the item belongs to, or null.</param>
internal sealed record FixTarget(string Slug, string Link, string HeadRepo, string Branch, bool PostsThread, RepoName? Repo);
