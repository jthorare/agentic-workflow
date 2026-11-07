namespace Aw.Sindri.Core.Skills;

/// <summary>The type of a skill block's input or output (skill flows spec §3.1): release 1 has text and path.</summary>
public enum SkillValueType
{
    /// <summary>Free text.</summary>
    Text = 0,

    /// <summary>An absolute path the session can read.</summary>
    Path = 1,
}
