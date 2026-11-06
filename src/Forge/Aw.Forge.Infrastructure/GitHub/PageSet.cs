namespace Aw.Forge.Infrastructure.GitHub;

/// <summary>The pages GitHubApi.Pages read.</summary>
/// <param name="Replies">One reply per page, in order.</param>
/// <param name="More">Whether a next page was left unread.</param>
internal sealed record PageSet(IReadOnlyList<GitHubReply> Replies, bool More);
