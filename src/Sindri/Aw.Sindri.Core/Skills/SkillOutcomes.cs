namespace Aw.Sindri.Core.Skills;

/// <summary>A block's outcomes (skill flows spec §3.1): the result key they come from and the map to camelCase names.</summary>
/// <param name="From">The result key ('verdict', 'status'); null when the only outcome is the success one.</param>
/// <param name="Map">Result value to outcome name.</param>
/// <param name="Success">The one success outcome ('pass', 'done').</param>
public sealed record SkillOutcomes(string? From, IReadOnlyDictionary<string, string> Map, string Success);
