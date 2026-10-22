namespace Aw.Sindri.Core.Forge;

/// <summary>A review thread, a review body or a top-level comment of a pull request. Every text in it is untrusted.</summary>
/// <param name="Key">The forge's stable key for it; a reply names it.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Association">The author's relation to the repository (such as MEMBER or CONTRIBUTOR).</param>
/// <param name="IsBot">Whether the author is a bot.</param>
/// <param name="Resolved">Whether a thread is resolved.</param>
/// <param name="Outdated">Whether the code a thread sits on has changed.</param>
/// <param name="Path">The file an inline thread is on, or null.</param>
/// <param name="Body">The text.</param>
/// <param name="Markers">The reply markers already in it.</param>
/// <param name="CreatedAt">When it was written. Threads come oldest first by this.</param>
/// <param name="Line">The line an inline thread sits on, or null.</param>
/// <param name="EditedAt">When it was last edited, or null.</param>
/// <param name="Url">Its link on the forge, or null.</param>
public sealed record PullThread(string Key, PullCommentKind Kind, string Author, string Association, bool IsBot, bool Resolved, bool Outdated, string? Path, string Body, IReadOnlyList<string> Markers, DateTimeOffset CreatedAt, int? Line, DateTimeOffset? EditedAt, string? Url);
