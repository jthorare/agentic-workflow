namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/skills (skill flows spec §8.1): the skill blocks Sindri loaded.</summary>
/// <param name="Blocks">One per loaded block.</param>
internal sealed record SkillsView(IReadOnlyList<SkillBlockView> Blocks);
