using Aw.Kernel.Testing;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Tests.Evolve;

/// <summary>The tier rules: inside its own target a proposal is code; every unclear case fails closed to approval.</summary>
[Lane(TestLane.Unit)]
public sealed class ProposalTiersTests
{
    [Fact]
    public void A_skill_edit_inside_its_own_skill_is_code() =>
        Assert.Equal(ProposalTier.Code, ProposalTiers.For(Proposals.Skill("review", "skills/review/SKILL.md", "skills/review/references/a.md")).Tier);

    [Fact]
    public void A_style_rule_inside_rules_engineering_is_code() =>
        Assert.Equal(ProposalTier.Code, ProposalTiers.For(Proposals.Of(ProposalKind.StyleRule, "rule:no-sleep", "rules/engineering/csharp/no-sleep.yaml")).Tier);

    [Fact]
    public void A_docs_change_inside_docs_is_code() =>
        Assert.Equal(ProposalTier.Code, ProposalTiers.For(Proposals.Of(ProposalKind.Docs, "doc:docs/csharp/sindri.md", "docs/csharp/sindri.md")).Tier);

    [Fact]
    public void A_leading_dot_slash_and_double_slash_normalize_and_stay_code() =>
        Assert.Equal(ProposalTier.Code, ProposalTiers.For(Proposals.Skill("review", "./skills//review/SKILL.md")).Tier);

    [Fact]
    public void A_dot_dot_segment_is_approval() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Skill("review", "skills/review/../../AGENTS.md")).Tier);

    [Fact]
    public void An_absolute_path_is_approval() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Skill("review", "/skills/review/SKILL.md")).Tier);

    [Fact]
    public void A_backslash_is_approval() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Skill("review", "skills\\review\\SKILL.md")).Tier);

    [Fact]
    public void An_empty_file_list_is_approval() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Skill("review")).Tier);

    [Fact]
    public void A_file_outside_the_target_is_approval() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Skill("review", "skills/review/SKILL.md", "skills/judge/SKILL.md")).Tier);

    [Fact]
    public void A_skill_whose_name_only_starts_like_the_target_is_outside_it() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Skill("review", "skills/reviewer/SKILL.md")).Tier);

    [Theory]
    [InlineData("evals/review/routing/a.json")]
    [InlineData("tests/Aw.Sindri.Tests/Evolve/x.cs")]
    [InlineData("scripts/skill-evals.sh")]
    [InlineData("src/Sindri/Aw.Sindri.Core/Evolve/LeakGate.cs")]
    [InlineData(".agents/rules/skills.md")]
    [InlineData("config/hooks/safety.sh")]
    [InlineData("AGENTS.md")]
    public void Eval_and_gate_machinery_is_approval_even_for_a_code_kind(string file) =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Of(ProposalKind.Code, "code:x", file)).Tier);

    [Fact]
    public void A_code_kind_is_always_approval() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Of(ProposalKind.Code, "code:x", "src/Forge/Aw.Forge.Core/A.cs")).Tier);

    [Fact]
    public void A_target_that_does_not_name_its_kind_is_approval() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Of(ProposalKind.SkillEdit, "rule:review", "skills/review/SKILL.md")).Tier);

    [Fact]
    public void A_style_rule_may_not_reach_into_evals() =>
        Assert.Equal(ProposalTier.Approval, ProposalTiers.For(Proposals.Of(ProposalKind.StyleRule, "rule:x", "rules/engineering/csharp/x.yaml", "evals/x/a.json")).Tier);

    [Fact]
    public void A_rewrite_that_reaches_outside_raises_the_public_tier()
    {
        var original = Proposals.Skill("review", "skills/review/SKILL.md");
        var rewrite = Proposals.Skill("review", "skills/review/SKILL.md", "AGENTS.md");

        Assert.Equal(ProposalTier.Approval, ProposalTiers.Public(original, rewrite).Tier);
    }

    [Fact]
    public void A_rewrite_never_lowers_the_public_tier()
    {
        var original = Proposals.Skill("review", "skills/review/SKILL.md", "AGENTS.md");
        var rewrite = Proposals.Skill("review", "skills/review/SKILL.md");

        Assert.Equal(ProposalTier.Approval, ProposalTiers.Public(original, rewrite).Tier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("skills/review/")]
    [InlineData("skills/./review/SKILL.md")]
    [InlineData("C:/skills/review/SKILL.md")]
    [InlineData("skills/review/SKILL.md\n")]
    [InlineData("~/skills/review/SKILL.md")]
    public void An_unsafe_path_does_not_normalize(string path) => Assert.Null(RepoPath.Normalize(path));

    [Fact]
    public void A_safe_path_normalizes_without_dot_slash_and_repeats() =>
        Assert.Equal("skills/review/SKILL.md", RepoPath.Normalize("./skills//review/SKILL.md"));
}
