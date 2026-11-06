namespace Aw.Sindri.Core.Skills;

/// <summary>The pure decision behind the result read at each turn end (skill flows spec §3.3).</summary>
public static class SkillResultReader
{
    /// <summary>Decides what the turn end means.</summary>
    /// <param name="input">What the reader looks at.</param>
    /// <returns>The decision.</returns>
    public static SkillResultDecision Decide(SkillResultInput input) => throw new NotSupportedException();
}
