using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>Send a finding of a PR as a fix task (POST /sindri/reviews/{owner}/{repo}/{number}/fix; PR review page spec §8.2): an item in Work, a thread on the PR when it is the owner's or an agent's, and the lane that owns the branch. Human only.</summary>
/// <param name="Owner">The PR's repository owner.</param>
/// <param name="Repo">The PR's repository name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Kind">check, requirement or line.</param>
/// <param name="Text">What needs fixing: 1 to 2,000 characters; the item's title is its first line.</param>
/// <param name="Head">The head sha the finding is on: 40 hex digits.</param>
/// <param name="Source">The check or requirement id; null for a line.</param>
/// <param name="Path">The file of a diff line; null otherwise.</param>
/// <param name="Line">The line of a diff line; null otherwise.</param>
/// <param name="Side">new or old; null means new. A removed line is old, and its Line is the old file's number.</param>
/// <param name="Item">The item a Retry reuses (the same marker is posted once); null on a first send.</param>
public sealed record SendReviewFixRequest(string Owner, string Repo, int Number, string Kind, string Text, string Head, string? Source, string? Path, int? Line, string? Side, string? Item)
    : ICommand<SendReviewFixResponse>;
