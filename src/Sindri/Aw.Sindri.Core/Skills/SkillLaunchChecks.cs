using Aw.Kernel;

namespace Aw.Sindri.Core.Skills;

/// <summary>The two checks a skill launch passes before any session starts (skill flows spec §3.1 and ADDED 2026-11-05): everything its requires.yaml names exists, and the agent will run the SKILL.md Sindri loaded.</summary>
public static class SkillLaunchChecks
{
    /// <summary>SND-SKILL-005: names each missing bin, MCP server, skill and agent with its fix.</summary>
    /// <param name="skill">The skill's folder name.</param>
    /// <param name="requires">Its requires.yaml.</param>
    /// <param name="home">The agent home the launch uses.</param>
    /// <returns>The error, or null when everything is there.</returns>
    public static Error? Requirements(string skill, SkillRequires requires, IAgentHome home)
    {
        ArgumentNullException.ThrowIfNull(requires);
        ArgumentNullException.ThrowIfNull(home);
        List<string> missing = [];
        missing.AddRange(requires.Bins.Where(b => !home.HasBin(b)).Select(static b => $"bin {b} (install it so it is on PATH)"));
        missing.AddRange(requires.Mcp.Where(m => !home.HasMcp(m)).Select(static m => $"MCP server {m} (register it with the agent; ./setup.sh registers the toolkit's servers)"));
        missing.AddRange(requires.Skills.Where(s => !home.HasSkill(s)).Select(s => $"skill {s} (run ./setup.sh so {home.Folder}/skills/{s} links this toolkit)"));
        missing.AddRange(requires.Agents.Where(a => !home.HasAgent(a)).Select(static a => $"agent {a} (run ./setup.sh --install-agents)"));
        return missing.Count == 0 ? null : SindriErrors.SkillRequirementMissing.With($"{skill} needs {string.Join("; ", missing)}");
    }

    /// <summary>SND-SKILL-007: the agent home's SKILL.md must be the file Sindri loaded, so the skill the ledger records is the one that ran.</summary>
    /// <param name="skill">The skill's folder name.</param>
    /// <param name="loadedPath">The SKILL.md Sindri loaded (beside the block.yaml).</param>
    /// <param name="loadedSha">Its SHA-256.</param>
    /// <param name="home">The agent home the launch uses.</param>
    /// <returns>The error, or null when the sha matches.</returns>
    public static Error? AgentCopy(string skill, string loadedPath, string loadedSha, IAgentHome home)
    {
        ArgumentNullException.ThrowIfNull(home);
        var (path, sha) = home.SkillMarkdown(skill);
        if (string.Equals(sha, loadedSha, StringComparison.Ordinal))
        {
            return null;
        }

        var found = sha is null ? $"{path} is missing" : $"{path} has sha {sha}";
        return SindriErrors.SkillAgentCopyDiffers.With(
            $"{skill}: the agent would not run the SKILL.md Sindri loaded: {found}, Sindri loaded {loadedPath} (sha {loadedSha}). "
            + "Fix: re-run setup.sh so the agent home links this toolkit, or point AW_SINDRI_SKILLS_DIR at the folder the agent reads. "
            + "If your toolkit checkout is behind (not pulled), this fails every run until you update it and re-run setup.sh: that's on purpose, so the recorded skill is the one that ran.");
    }
}
