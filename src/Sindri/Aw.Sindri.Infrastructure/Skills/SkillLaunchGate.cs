using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>Runs a skill launch's two checks before any session starts (SND-SKILL-005 and SND-SKILL-007): the skill's requires.yaml beside its block.yaml, and the agent home's SKILL.md against the one Sindri loaded.</summary>
internal static class SkillLaunchGate
{
    /// <summary>Checks the launch.</summary>
    /// <param name="skill">The skill launch; one that carries no manifest path is built by hand (a test) and passes.</param>
    /// <param name="home">The agent home the launch uses.</param>
    /// <returns>The first error, or null when the launch may start.</returns>
    internal static Error? Check(SkillLaunch skill, IAgentHome home)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(home);
        if (skill.ManifestPath is not { } manifest)
        {
            return null;
        }

        var requires = Requires(skill.Skill, Path.Combine(Path.GetDirectoryName(manifest) ?? string.Empty, "requires.yaml"));
        if (requires.Error is { } unreadable)
        {
            return unreadable;
        }

        var missing = SkillLaunchChecks.Requirements(skill.Skill, requires.Value, home);
        if (missing is not null || skill.TrialSkillsDirectory is not null || skill.SkillMdSha is not { } sha)
        {
            return missing;   // why: a trial reads the skill by path from its own folder, so the agent home's copy is not what runs
        }

        return SkillLaunchChecks.AgentCopy(skill.Skill, Path.Combine(Path.GetDirectoryName(manifest) ?? string.Empty, "SKILL.md"), sha, home);
    }

    // A skill with no requires.yaml requires nothing; one that can't be read fails the launch with the reason (the repo's own gate keeps the files well formed).
    private static (SkillRequires Value, Error? Error) Requires(string skill, string path)
    {
        if (!File.Exists(path))
        {
            return (SkillRequires.None, null);
        }

        try
        {
            var parsed = SkillRequires.Parse(File.ReadAllText(path), out var problem);
            return (parsed, problem is null ? null : SindriErrors.SkillRequirementMissing.With($"{skill}: {path} {problem}"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (SkillRequires.None, SindriErrors.SkillRequirementMissing.With($"{skill}: {path} can't be read: {e.Message}"));
        }
    }
}
