using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The price table and the budget facts (screens spec E-28): dollars only when every model is priced, tokens otherwise.</summary>
[Lane(TestLane.Unit)]
public sealed class BudgetsTests
{
    private const long Million = 1_000_000;
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static AgentSettings Agents(decimal budget, decimal? soft = null) =>
        new(soft, new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [_claude.Value] = new(DailyBudget: budget) });

    private static AgentSpend Spent(decimal usd) => new(usd, Tokens: 1, SpendBasis.PriceTable);

    [Fact]
    public void A_model_is_priced_by_its_longest_prefix()
    {
        var sonnet = Budgets.PriceOf("claude-sonnet-4-5-20250929");
        var newerOpus = Budgets.PriceOf("claude-opus-4-5");
        var olderOpus = Budgets.PriceOf("claude-opus-4-1-20250805");

        Expect.Equal((3m, 15m), (sonnet!.Input, sonnet.Output));
        Expect.Equal(5m, newerOpus!.Input);
        Expect.Equal(15m, olderOpus!.Input);
        Assert.Null(Budgets.PriceOf("mystery-9"));
    }

    [Fact]
    public void Spend_prices_each_kind_of_token()
    {
        var spend = Budgets.Spend([new ModelTokens("claude-sonnet-4-5", Input: Million, CacheRead: Million, CacheWrite: Million, Output: Million)]);

        Expect.Equal((3m + 0.30m + 3.75m + 15m, 4 * Million, SpendBasis.PriceTable), (spend.Usd, spend.Tokens, spend.Basis));
    }

    [Fact]
    public void Tokens_only_when_model_unpriced()
    {
        var spend = Budgets.Spend([new ModelTokens("claude-sonnet-4-5", Million, 0, 0, 0), new ModelTokens("mystery-9", 500, 0, 0, 500)]);

        Expect.Equal((3m, Million + 1000, SpendBasis.TokensOnly), (spend.Usd, spend.Tokens, spend.Basis));
    }

    [Fact]
    public void No_usage_is_priced_at_nothing() => Expect.Equal(new AgentSpend(0m, 0, SpendBasis.PriceTable), Budgets.Spend([]));

    [Theory]
    [InlineData("7.99", LaunchBudgetState.Ok)]
    [InlineData("8.00", LaunchBudgetState.Soft)]
    [InlineData("9.99", LaunchBudgetState.Soft)]
    [InlineData("10.00", LaunchBudgetState.Hard)]
    public void A_budget_is_ok_soft_or_hard_by_its_fraction(string spent, LaunchBudgetState state)
    {
        var facts = Budgets.For(Agents(10m), _claude, Spent(decimal.Parse(spent, System.Globalization.CultureInfo.InvariantCulture)));

        Expect.Equal((state, 10m), (facts.State, facts.DailyUsd));
        Expect.Equal(decimal.Parse(spent, System.Globalization.CultureInfo.InvariantCulture) / 10m, facts.Fraction);
        Expect.Equal(SpendBasis.PriceTable, facts.Basis);
    }

    [Fact]
    public void The_soft_limit_comes_from_the_profile() => Expect.Equal(LaunchBudgetState.Soft, Budgets.For(Agents(10m, soft: 0.5m), _claude, Spent(5m)).State);

    [Fact]
    public void No_budget_is_none_but_still_shows_the_spend()
    {
        var facts = Budgets.For(agents: null, _claude, Spent(3m));

        Expect.Equal((LaunchBudgetState.None, 3m, null), (facts.State, facts.SpentUsd, facts.DailyUsd));
        Assert.Null(facts.Fraction);
    }

    [Fact]
    public void A_budget_with_no_usage_reader_has_nothing_to_count_by()
    {
        var facts = Budgets.For(Agents(10m), _claude, spend: null);

        Expect.Equal(LaunchBudgetState.NoUsageReader, facts.State);
        Assert.Null(facts.SpentUsd);
        Assert.Null(facts.Basis);
    }

    [Fact]
    public void A_tokens_only_spend_applies_no_limit()
    {
        var facts = Budgets.For(Agents(10m), _claude, new AgentSpend(Usd: 99m, Tokens: 5, SpendBasis.TokensOnly));

        Expect.Equal((LaunchBudgetState.Ok, SpendBasis.TokensOnly, 5L), (facts.State, facts.Basis, facts.Tokens));
        Assert.Null(facts.Fraction);
    }

    [Fact]
    public void The_total_adds_every_target()
    {
        var agents = Agents(10m);
        var claude = Budgets.For(agents, _claude, Spent(6m));
        var codex = Budgets.For(agents, AgentId.From("codex"), new AgentSpend(2m, 7, SpendBasis.TokensOnly));

        var total = Budgets.Total(agents, [claude, codex]);

        Expect.Equal((10m, 8m, SpendBasis.TokensOnly, 8L), (total.DailyUsd, total.SpentUsd, total.Basis, total.Tokens));
        Expect.Equal(LaunchBudgetState.Ok, total.State);   // tokens only: no limit is applied
    }

    [Fact]
    public void The_total_is_soft_when_every_spend_is_priced_and_the_sum_is_near_the_budget()
    {
        var agents = Agents(10m);

        var total = Budgets.Total(agents, [Budgets.For(agents, _claude, Spent(6m)), Budgets.For(agents, AgentId.From("codex"), Spent(2m))]);

        Expect.Equal((LaunchBudgetState.Soft, SpendBasis.PriceTable, 0.8m), (total.State, total.Basis, total.Fraction));
    }
}
