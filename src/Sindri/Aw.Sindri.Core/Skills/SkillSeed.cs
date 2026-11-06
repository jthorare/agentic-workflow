using System.Text;

namespace Aw.Sindri.Core.Skills;

/// <summary>The seed a skill session gets (skill flows spec D2, §3.1 "args grammar").</summary>
public static class SkillSeed
{
    private const int MaxInlineText = 500;

    /// <summary>The whole seed: only the skill's invocation in the target's form, one line, with each input placed as one token, a path with a space double-quoted, an empty optional fragment dropped, and a text that is long, has a newline or starts with '/' or '$' replaced by '(the text in &lt;run dir&gt;/&lt;key&gt;.json)'. A trial with a skills folder reads the skill by path instead of by name.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="runDirectory">The run directory the inputs are written into as '&lt;key&gt;.json'.</param>
    /// <returns>The seed.</returns>
    public static string Render(SkillLaunch launch, string runDirectory)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(runDirectory);
        var arguments = string.Join(' ', launch.Block.Args.Select(a => Token(launch, a, runDirectory)).Where(static t => t.Length > 0));
        if (launch.TrialSkillsDirectory is { } folder)
        {
            var read = $"Read {folder.TrimEnd('/')}/{launch.Skill}/SKILL.md and follow it";
            return arguments.Length == 0 ? read : $"{read} with arguments: {arguments}";
        }

        var invocation = (launch.Target == SkillTarget.Codex ? "$" : "/") + launch.Skill;
        return arguments.Length == 0 ? invocation : invocation + " " + arguments;
    }

    // A text item fills its '{input}' slots; a fragment is its flag and value, or nothing when the optional input is empty.
    private static string Token(SkillLaunch launch, SkillArg arg, string runDirectory)
    {
        if (arg.Text is { } text)
        {
            var filled = new StringBuilder(text);
            foreach (var input in launch.Block.Inputs)
            {
                _ = filled.Replace("{" + input.Key + "}", Value(launch, input, runDirectory));
            }

            return filled.ToString();
        }

        var fragmentInput = launch.Block.Inputs.FirstOrDefault(i => string.Equals(i.Key, arg.Input, StringComparison.Ordinal));
        return fragmentInput is null || !launch.Inputs.TryGetValue(fragmentInput.Key, out var set) || string.IsNullOrWhiteSpace(set)
            ? string.Empty
            : $"{arg.Flag} {Value(launch, fragmentInput, runDirectory)}";
    }

    private static string Value(SkillLaunch launch, SkillInput input, string runDirectory)
    {
        var value = launch.Inputs.GetValueOrDefault(input.Key) ?? string.Empty;
        if (input.Type == SkillValueType.Text)
        {
            // why: long or risky text can't sit in a one-line command: the run dir already holds it as '<key>.json' (spec §3.1)
            return value.Length > MaxInlineText || value.Contains('\n', StringComparison.Ordinal) || value.StartsWith('/') || value.StartsWith('$')
                ? $"(the text in {runDirectory.TrimEnd('/')}/{input.Key}.json)"
                : value;
        }

        return value.Any(char.IsWhiteSpace) ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"" : value;
    }
}
