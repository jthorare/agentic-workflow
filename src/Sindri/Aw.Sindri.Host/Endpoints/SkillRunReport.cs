namespace Aw.Sindri.Host.Endpoints;

/// <summary>How a followed 'sindri skill run' ended (skill flows spec §7).</summary>
/// <param name="Skill">The block's skill.</param>
/// <param name="ManifestHash">The manifest hash the run used, printed first.</param>
/// <param name="Outcome">The outcome name; null when the Step failed.</param>
/// <param name="Succeeded">True when the outcome is the block's success one.</param>
/// <param name="Facts">The facts the run wrote.</param>
/// <param name="ResultPath">The result file's path.</param>
/// <param name="ErrorLine">The 'SND-SKILL-00x ...' line of a failed Step; null otherwise.</param>
internal sealed record SkillRunReport(string Skill, string ManifestHash, string? Outcome, bool Succeeded, IReadOnlyList<SkillRunFact> Facts, string ResultPath, string? ErrorLine);
