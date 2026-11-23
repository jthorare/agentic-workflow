using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Tests.Evolve;

/// <summary>Proposal builders for the evolve tests.</summary>
internal static class Proposals
{
    public static Proposal Skill(string name, params string[] files) =>
        new(ProposalKind.SkillEdit, $"skill:{name}", "Run the tests before saying done", "The human asked twice for proof.", new ProposalChange(files, "Add a step."), ["transcript:1a2b3c4d#12"]);

    public static Proposal Of(ProposalKind kind, string target, params string[] files) =>
        new(kind, target, "A general title", "A general reason.", new ProposalChange(files, "A general change."), ["transcript:1a2b3c4d#12"]);

    public static Proposal Saying(string title, string rationale = "A general reason.", string description = "A general change.") =>
        new(ProposalKind.SkillEdit, "skill:review", title, rationale, new ProposalChange(["skills/review/SKILL.md"], description), ["transcript:1a2b3c4d#12"]);
}
