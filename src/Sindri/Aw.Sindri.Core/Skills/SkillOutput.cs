namespace Aw.Sindri.Core.Skills;

/// <summary>One output of a skill block: a fact read from the skill's result file (skill flows spec §3.1).</summary>
/// <param name="Key">The output's name; the fact key is 'fact:&lt;idCamel&gt;&lt;Key capitalised&gt;'.</param>
/// <param name="Type">Text or path.</param>
/// <param name="From">A top-level result key ('feature_dir', 'verdict') or 'artifacts[&lt;file name&gt;]'.</param>
public sealed record SkillOutput(string Key, SkillValueType Type, string From);
