namespace Aw.Sindri.Core.Todos;

/// <summary>A hold pick still inside its Undo window (todo spec §4.5 "The hold"): which pick it was and what each applied todo was before it.</summary>
/// <param name="At">When the pick was made.</param>
/// <param name="Kept">'applied' or 'restored'.</param>
/// <param name="Priors">Each todo Apply changed: its row before, and the version Apply left it at. Empty for 'restored'.</param>
public sealed record TodoHoldPick(DateTimeOffset At, string Kept, IReadOnlyList<(TodoRow Prior, long VersionAfter)> Priors);
