namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>What a fix task did (§8.2): the item it made and where it went.</summary>
/// <param name="Item">The item's id.</param>
/// <param name="Went">babysitter, session or waiting: who took the task.</param>
/// <param name="Branch">The head branch ("Waiting on feat/slot-hold").</param>
/// <param name="Thread">The posted thread's link; null when none was posted (a contributor's PR).</param>
/// <param name="Session">The session that took it ("claude, ~/repos/aw-wt/harbor-slot-hold"); null unless <paramref name="Went"/> is session.</param>
/// <param name="PostCode">SND-REVIEW-006 when the item was made but the thread wasn't posted (the half-done send); null otherwise.</param>
/// <param name="PostError">The half-done text ("Item 01J9… created in Work; posting to #318 failed: GitHub answered 502"); null otherwise.</param>
internal sealed record ReviewFixSent(string Item, string Went, string Branch, string? Thread, string? Session, string? PostCode, string? PostError);
