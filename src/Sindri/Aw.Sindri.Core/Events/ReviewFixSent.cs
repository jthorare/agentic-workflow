using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'review.fix-sent' payload (PR review page spec §8.2 step 4): a finding was sent as a fix task. Key 'review.fix-sent:&lt;item&gt;:&lt;commandId&gt;'.</summary>
/// <param name="CommandId">The command that sent it.</param>
/// <param name="Item">The Work item made (or reused by a retry).</param>
/// <param name="Pr">The pull request's number.</param>
/// <param name="Thread">The posted thread's link, or null when none was posted.</param>
/// <param name="Went">babysitter, session or waiting.</param>
/// <param name="Failure">The error code when posting failed (SND-REVIEW-006), else null.</param>
public sealed record ReviewFixSent(CommandId CommandId, ItemId Item, int Pr, string? Thread, string Went, string? Failure);
