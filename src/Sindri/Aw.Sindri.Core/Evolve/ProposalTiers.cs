using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Evolve;

/// <summary>The tier rules: computed from the files a proposal touches, failing closed to <see cref="ProposalTier.Approval"/>.</summary>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "The proposal store and the proposal_stage block (issue 3787, pieces 3b and 3c) call it; until then only the tests do.")]
internal static class ProposalTiers
{
    // why: the machinery that judges a proposal; a proposal that edits it would grade itself
    private static readonly string[] _machinery = ["evals/", "tests/", ".agents/rules/", "config/hooks/", "scripts/skill-evals.sh", "AGENTS.md", "CLAUDE.md"];

    /// <summary>The tier of one proposal.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <returns>The tier and why.</returns>
    public static TierDecision For(Proposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.Change.Files.Count == 0)
        {
            return Approval("names no file");
        }

        var files = proposal.Change.Files.Select(RepoPath.Normalize).ToList();
        if (files.Exists(static f => f is null))
        {
            return Approval("names a path that doesn't normalize");
        }

        if (files.Exists(static f => IsMachinery(f ?? string.Empty)))
        {
            return Approval("changes the machinery that evaluates or gates it");
        }

        if (Root(proposal) is not { } root)
        {
            return Approval("its kind always needs approval, or its target doesn't name its kind");
        }

        return files.TrueForAll(f => (f ?? string.Empty).StartsWith(root, StringComparison.Ordinal))
            ? new TierDecision(ProposalTier.Code, "a change inside its own target, built and merged as an ordinary work item")
            : Approval("touches files outside its target");
    }

    /// <summary>The tier a public version ships at: the stricter of the original's and the rewrite's, so a rewrite never lowers it.</summary>
    /// <param name="original">The local original.</param>
    /// <param name="rewrite">The public rewrite.</param>
    /// <returns>The stricter decision.</returns>
    public static TierDecision Public(Proposal original, Proposal rewrite)
    {
        var first = For(original);
        var second = For(rewrite);
        return second.Tier > first.Tier ? second : first;
    }

    private static TierDecision Approval(string why) => new(ProposalTier.Approval, why);

    private static bool IsMachinery(string file) =>
        _machinery.Any(m => m.EndsWith('/') ? file.StartsWith(m, StringComparison.Ordinal) : file.Equals(m, StringComparison.Ordinal))
        || (file.StartsWith("src/Sindri/", StringComparison.Ordinal) && file.Split('/').Contains("Evolve", StringComparer.Ordinal));

    // why: the folder a target owns; null for the code kind, a malformed target or one whose prefix names another kind
    private static string? Root(Proposal proposal)
    {
        var (prefix, folder) = proposal.Kind switch
        {
            ProposalKind.SkillEdit => ("skill:", "skills/"),
            ProposalKind.StyleRule => ("rule:", "rules/engineering/"),
            ProposalKind.Docs => ("doc:", "docs/"),
            ProposalKind.Code => (string.Empty, string.Empty),
            _ => (string.Empty, string.Empty),
        };
        if (prefix.Length == 0 || !proposal.Target.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = proposal.Target[prefix.Length..];
        var slash = proposal.Kind == ProposalKind.Docs;
        if (name.Length == 0 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' || (slash && c == '/')))
        {
            return null;
        }

        return proposal.Kind == ProposalKind.SkillEdit ? $"{folder}{name}/" : folder;
    }
}
