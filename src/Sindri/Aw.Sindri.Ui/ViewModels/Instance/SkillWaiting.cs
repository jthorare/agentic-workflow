namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>A `launch.owner-waiting` event (skill flows spec §7) as the card reads it.</summary>
/// <param name="Reason">'question', 'noResult', 'unreadable' or 'skillUpdated'.</param>
/// <param name="Text">The skill's question; null for every other reason.</param>
/// <param name="Detail">The parse error or the other skill's name; null otherwise.</param>
/// <param name="ResultPath">The result file the reader looked at.</param>
/// <param name="State">The state's id in the instance.</param>
/// <param name="Attempt">The Step run's number.</param>
internal sealed record SkillWaiting(string Reason, string? Text, string? Detail, string ResultPath, string? State, int? Attempt);
