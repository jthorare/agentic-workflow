namespace Aw.Sindri.Core.Skills;

/// <summary>Why a skill Step is waiting on the owner; the 'reason' of launch.owner-waiting.</summary>
public enum SkillWaitReason
{
    /// <summary>The result carries a 'waiting' question.</summary>
    Question = 0,

    /// <summary>No result file, or one written before this launch started.</summary>
    NoResult = 1,

    /// <summary>The file isn't valid JSON, or names another skill (SND-SKILL-001, SND-SKILL-002).</summary>
    Unreadable = 2,

    /// <summary>A pushing block's approval drifted (reader, F-2a).</summary>
    SkillUpdated = 3,
}
