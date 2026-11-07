using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Skills;

/// <summary>What the watch of a skill block's session is given (skill flows spec §3.3): the launch, the block to read its result against, and the item and Step the 'launch.owner-waiting' events are about.</summary>
/// <param name="Launch">The launch ('LCH-…').</param>
/// <param name="Item">The item.</param>
/// <param name="Skill">The skill's folder name; the result's 'skill' must match it.</param>
/// <param name="Block">The block's manifest entry.</param>
/// <param name="RunDirectory">The launch's run directory, which holds '.sindri-out/result-&lt;launch&gt;.json'.</param>
/// <param name="Started">When the launch started; a result file written before it is not this launch's.</param>
/// <param name="Instance">The workflow instance, or null.</param>
/// <param name="State">The Step's state id, or null.</param>
/// <param name="Attempt">The Step run's number, or null.</param>
public sealed record SkillWatch(string Launch, ItemId Item, string Skill, SkillBlockManifest Block, string RunDirectory, DateTimeOffset Started, string? Instance = null, string? State = null, int? Attempt = null);
