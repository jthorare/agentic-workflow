namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>POST /sindri/reviews/{owner}/{repo}/{number}/fix (review page spec §8.2; spec-shaped until the API has a golden): the finding to send as a fix task.</summary>
/// <param name="Kind">check, requirement or line.</param>
/// <param name="Text">What needs fixing (up to 2,000 characters); the item's title is its first line.</param>
/// <param name="Head">The head sha the finding is on.</param>
/// <param name="SourceId">The check or requirement id; null for a line.</param>
/// <param name="Path">The file of a diff line; null otherwise.</param>
/// <param name="Line">The line of a diff line; null otherwise.</param>
/// <param name="Side">new or old: the side of the diff the line is on; null means new. A removed line is old, and its Line is the old file's number.</param>
/// <param name="Item">The item a Retry post reuses (so the same marker is posted once); null on a first send.</param>
internal sealed record ReviewFixSend(string Kind, string Text, string Head, string? SourceId, string? Path, int? Line, string? Side, string? Item);
