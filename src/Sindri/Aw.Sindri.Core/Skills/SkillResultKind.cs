namespace Aw.Sindri.Core.Skills;

/// <summary>What a turn end means for a skill Step (skill flows spec §3.3 table).</summary>
public enum SkillResultKind
{
    /// <summary>A finished result: outputs to facts, verdict to outcome.</summary>
    Complete = 0,

    /// <summary>Waiting on the owner (see <see cref="SkillWaitReason"/>).</summary>
    OwnerWaiting = 1,

    /// <summary>The same file as at the last turn end: still waiting on the owner, nothing new to say.</summary>
    StillWaiting = 2,

    /// <summary>step.failed: a block-author bug (SND-SKILL-003, SND-SKILL-004).</summary>
    StepFailed = 3,

    /// <summary>Working, no new result: the file is older than the launch's last prompt. No row, nothing to say.</summary>
    Working = 4,
}
