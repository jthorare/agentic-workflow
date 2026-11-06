namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>What a fix task did (§8.2); the API always answers 200 with these fields.</summary>
/// <param name="ItemId">The item made in Work.</param>
/// <param name="Posted">True when the thread was posted; false for a half-done send and for a contributor's PR (item only).</param>
/// <param name="PostCode">SND-REVIEW-006 on a half-done send; null otherwise.</param>
/// <param name="PostError">The half-done text ("Item 01J9… created in Work; posting to #318 failed: GitHub answered 502"); null otherwise.</param>
/// <param name="Went">babysitter, session or waiting: who took the task. NOT in the API's confirmed shape; null when absent (the sheet then says only that the item waits in Work).</param>
/// <param name="Session">The session that took it ("claude, ~/repos/aw-wt/harbor-slot-hold"); NOT in the confirmed shape; null when absent.</param>
internal sealed record ReviewFixSent(string ItemId, bool Posted, string? PostCode, string? PostError, string? Went = null, string? Session = null);
