using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>Plants a conversation item in a fake or file forge, as if another account had written it.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="Kind">Thread, review body or comment.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Association">OWNER, MEMBER, CONTRIBUTOR, NONE ...</param>
/// <param name="IsBot">Whether the author is a bot.</param>
/// <param name="Path">The file, for an inline thread.</param>
/// <param name="Body">The text.</param>
/// <param name="CreatedAt">When it was written; the forge's clock at seeding when null.</param>
/// <param name="Line">The line, for an inline thread.</param>
/// <param name="EditedAt">When it was last edited, or null.</param>
public sealed record SeedThreadRequest(ForgeRef Pull, CommentKind Kind, string Author, string Association, bool IsBot, string? Path, string Body, DateTimeOffset? CreatedAt = null, int? Line = null, DateTimeOffset? EditedAt = null);
