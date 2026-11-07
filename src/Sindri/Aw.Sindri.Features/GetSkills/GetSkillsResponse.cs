using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.GetSkills;

/// <summary>The loaded skill blocks and the ones that did not load (skill flows spec §7).</summary>
/// <param name="Blocks">The loaded blocks, sorted by id.</param>
/// <param name="NotLoaded">The blocks skipped, with the doctor reason.</param>
internal sealed record GetSkillsResponse(IReadOnlyList<SkillBlockView> Blocks, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SkillNotLoadedView>? NotLoaded = null);
