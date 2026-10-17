namespace Aw.Forge.Core.Pulls;

/// <summary>One item of the conversation, as ListThreads returns it. The body is untrusted text.</summary>
/// <param name="Key">The adapter's handle for replying and reacting: thread:N, review:N or comment:N.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Association">The author's association with the repository (OWNER, MEMBER, CONTRIBUTOR, NONE ...).</param>
/// <param name="IsBot">Whether the author is a bot.</param>
/// <param name="Resolved">Whether the thread is resolved.</param>
/// <param name="Outdated">Whether the code it points at has changed.</param>
/// <param name="Path">The file an inline thread is on.</param>
/// <param name="Body">The opening text.</param>
/// <param name="Markers">The idempotency markers found anywhere in the thread, replies included.</param>
public sealed record ForgeThread(
    string Key,
    CommentKind Kind,
    string Author,
    string Association,
    bool IsBot,
    bool Resolved,
    bool Outdated,
    string? Path,
    string Body,
    IReadOnlyList<string> Markers);
