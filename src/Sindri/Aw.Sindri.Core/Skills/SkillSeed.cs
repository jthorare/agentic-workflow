namespace Aw.Sindri.Core.Skills;

/// <summary>The seed a skill session gets (skill flows spec D2, §3.1 "args grammar").</summary>
public static class SkillSeed
{
    /// <summary>The whole seed: only the skill's invocation in the target's form, one line, with each input placed as one token, a value with a space double-quoted, an empty optional fragment dropped, and a value that is long, has a newline or starts with '/' or '$' replaced by '(the text in &lt;run dir&gt;/&lt;key&gt;.json)'.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="runDirectory">The run directory the inputs are written into as '&lt;key&gt;.json'.</param>
    /// <returns>The seed.</returns>
    public static string Render(SkillLaunch launch, string runDirectory) => throw new NotSupportedException();
}
