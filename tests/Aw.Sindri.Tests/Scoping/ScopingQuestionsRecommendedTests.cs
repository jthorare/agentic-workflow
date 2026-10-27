using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// #1204: the scoping interview's questions (fact:questions) need exactly one recommended option each, the way a design decision does (DSN-DEC-001).
/// SCP-QST-001 is the expected rule id; a question that breaks it is a finding on that question, found before the interview desk opens.
/// </summary>
public sealed class ScopingQuestionsRecommendedTests
{
    private const string Rule = "SCP-QST-001";

    private static async Task<ScopingCheckResponse> Draft(ScopingFacts facts) =>
        (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Draft), CancellationToken.None)).Value;

    private static ScopingFacts WithRecommended(string question, params bool[] flags) =>
        ScopingFacts.Expiry().Edit("questions", n =>
        {
            var options = n.AsArray().Single(q => string.Equals((string?)q!["id"], question, StringComparison.Ordinal))!["options"]!.AsArray();
            for (var i = 0; i < flags.Length; i++)
            {
                options[i]!["recommended"] = flags[i];
            }
        });

    [Fact, Trait("Lane", "unit")]
    public async Task A_question_with_no_recommended_option_is_refused()
    {
        var response = await Draft(WithRecommended("Q3", false, false));

        Assert.Contains(response.Findings, f => f.Rule == Rule && string.Equals(f.Item, "Q3", StringComparison.Ordinal));
        Assert.Equal(ScopeOutcome.Gaps, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_question_with_two_recommended_options_is_refused()
    {
        var response = await Draft(WithRecommended("Q3", true, true));

        Assert.Contains(response.Findings, f => f.Rule == Rule && string.Equals(f.Item, "Q3", StringComparison.Ordinal));
        Assert.Equal(ScopeOutcome.Gaps, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Questions_with_exactly_one_recommended_option_each_pass_the_rule()
    {
        var response = await Draft(ScopingFacts.Expiry());

        Assert.DoesNotContain(response.Findings, f => f.Rule == Rule);
        Assert.Contains(response.Rules, r => r.Rule == Rule && r.Status == ScopeRuleStatus.Pass);
    }
}
