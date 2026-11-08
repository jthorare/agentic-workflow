namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>What a fix task did (§8.2); the API always answers 200 with these fields.</summary>
/// <param name="ItemId">The item made in Work.</param>
/// <param name="Posted">True when the thread was posted; false for a half-done send and for a contributor's PR (item only).</param>
/// <param name="PostCode">SND-REVIEW-006 on a half-done send; null otherwise.</param>
/// <param name="PostError">The half-done text ("Item 01J9… created in Work; posting to #318 failed: GitHub answered 502"); null otherwise.</param>
/// <param name="Went">session or waiting: who took the task (an older Sindri may also say babysitter, which reads as waiting). always present.</param>
/// <param name="Session">The session that took it ("claude, ~/repos/aw-wt/harbor-slot-hold"); set only when <paramref name="Went"/> is session; null otherwise.</param>
internal sealed record ReviewFixSent(string ItemId, bool Posted, string? PostCode, string? PostError, string Went, string? Session);
