using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Tests.Proposals;

/// <summary>Proposals to save in the store and golden tests.</summary>
internal static class ProposalSeeds
{
    public static Proposal Original(string title, string target = "skill:review", params string[] evidence) =>
        new(ProposalKind.SkillEdit, target, title, "The human corrected this twice.", new ProposalChange(["skills/review/SKILL.md"], "Add a step."), evidence.Length == 0 ? ["transcript:1a2b3c4d#12"] : evidence);

    public static ProposalToSave Saving(string title, string target = "skill:review", string session = "transcript:1a2b3c4d", params string[] turns) =>
        new(Original(title, target), ProposalTier.Code, [new LeakSource(session, turns.Length == 0 ? ["No, run the tests first."] : turns)], "workflow:steering-miner");
}
