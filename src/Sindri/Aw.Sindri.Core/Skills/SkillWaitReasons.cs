namespace Aw.Sindri.Core.Skills;

/// <summary>The wire names of <see cref="SkillWaitReason"/> ('launch.owner-waiting' carries them).</summary>
public static class SkillWaitReasons
{
    /// <summary>The name on the wire.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>'question', 'noResult', 'unreadable' or 'skillUpdated'.</returns>
    public static string Wire(this SkillWaitReason reason) => reason switch
    {
        SkillWaitReason.Question => "question",
        SkillWaitReason.NoResult => "noResult",
        SkillWaitReason.Unreadable => "unreadable",
        SkillWaitReason.SkillUpdated => "skillUpdated",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "not a SkillWaitReason"),
    };
}
