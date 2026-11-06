using Aw.Kernel;

namespace Aw.Sindri.Core;

// The skill block codes (skill flows spec §3.3): what a skill's result file can get wrong, and the requires.yaml check before launch.
public static partial class SindriErrors
{
    /// <summary>SND-SKILL-001: the skill's result file isn't valid JSON (waiting on you, with the parse error as 'detail').</summary>
    public static readonly ErrorDefinition SkillResultUnreadable = new("SND-SKILL-001", ErrorKind.Invalid, "The skill's result file can't be read");

    /// <summary>SND-SKILL-002: the result's 'skill' names another skill (waiting on you; 'detail' is the other skill's name).</summary>
    public static readonly ErrorDefinition SkillResultWrongSkill = new("SND-SKILL-002", ErrorKind.Conflict, "The result came from another skill");

    /// <summary>SND-SKILL-003: the result's verdict isn't in the block's outcome map (step.failed: a block-author bug).</summary>
    public static readonly ErrorDefinition SkillVerdictUnmapped = new("SND-SKILL-003", ErrorKind.Invalid, "The skill's verdict isn't mapped by its block.yaml");

    /// <summary>SND-SKILL-004: a mapped output is missing from the result, or a path output is relative, missing on disk or holds a newline (step.failed: a block-author bug).</summary>
    public static readonly ErrorDefinition SkillOutputInvalid = new("SND-SKILL-004", ErrorKind.Invalid, "The skill's result doesn't hold what its block.yaml declares");

    /// <summary>SND-SKILL-005: the skill's requires.yaml names a bin, MCP server, skill or agent the agent home lacks (step.failed before any session starts; 'detail' names each one and its fix).</summary>
    public static readonly ErrorDefinition SkillRequirementMissing = new("SND-SKILL-005", ErrorKind.Unavailable, "The skill needs something this machine doesn't have");

    /// <summary>SND-SKILL-006: a skill block's 'pr' input is neither a PR number nor a github.com pull request link (the host fetches the PR head before the launch).</summary>
    public static readonly ErrorDefinition SkillPrInputInvalid = new("SND-SKILL-006", ErrorKind.Invalid, "The pr input is not a PR number or link");

    /// <summary>SND-SKILL-007: the agent home's SKILL.md isn't the file Sindri loaded (step.failed before any session starts; 'detail' names both paths and both shas, and the fix).</summary>
    public static readonly ErrorDefinition SkillAgentCopyDiffers = new("SND-SKILL-007", ErrorKind.Conflict, "The agent would run a different SKILL.md than the one Sindri loaded");

    /// <summary>SND-SKILL-008: git could not fetch the pull request's head before a skill Step's launch (step.failed with git's own words, scrubbed; no session started).</summary>
    public static readonly ErrorDefinition SkillPrFetchFailed = new("SND-SKILL-008", ErrorKind.Unavailable, "The pull request could not be fetched");
}
