namespace Aw.Sindri.Host.Endpoints;

/// <summary>One fact a skill run wrote.</summary>
/// <param name="Key">The fact key ('fact:reviewFindings').</param>
/// <param name="Value">Its value.</param>
internal sealed record SkillRunFact(string Key, string Value);
