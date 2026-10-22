using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>'gate.pr.base-is-default' (PB spec §10.1, §10.2, BD4): passes only while the PR's base is the default branch the forge names.</summary>
[Lane(TestLane.Unit)]
public sealed class PrBaseIsDefaultGateTests
{
    private const string Link = "https://github.com/acme/app/pull/7";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static GateContext Of(string? pr, string? prState = null)
    {
        var facts = new Dictionary<FactKey, JsonElement>();
        if (pr is not null)
        {
            facts[FactKey.From("fact:pr")] = Json(pr);
        }

        if (prState is not null)
        {
            facts[FactKey.From("fact:prState")] = Json(prState);
        }

        return new GateContext(BlockId.From("gate.pr.base-is-default"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts, Threshold: null);
    }

    private static string PrOnBase(string baseRef) => $$$"""{"url":"{{{Link}}}","base":{"ref":"{{{baseRef}}}"}}""";

    private static FakePullRequests Forge(string defaultBranch = "main") => new(["github.com"]) { DefaultBranchName = defaultBranch };

    [Fact]
    public async Task A_base_that_is_the_default_branch_passes()
    {
        var result = await new PrBaseIsDefaultGate(Forge()).Evaluate(Of(PrOnBase("main")), Token);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public async Task A_base_that_is_another_branch_fails_and_names_both()
    {
        var result = await new PrBaseIsDefaultGate(Forge()).Evaluate(Of(PrOnBase("feat/phase-1")), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        var reason = Assert.Single(result.Reasons);
        Assert.Contains("feat/phase-1", reason, StringComparison.Ordinal);
        Assert.Contains("main", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_branch_that_only_starts_like_the_default_is_not_the_default()
    {
        var result = await new PrBaseIsDefaultGate(Forge()).Evaluate(Of(PrOnBase("main-2")), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    [Fact]
    public async Task The_forge_is_asked_about_the_repo_the_link_names()
    {
        var forge = Forge();

        _ = await new PrBaseIsDefaultGate(forge).Evaluate(Of(PrOnBase("main")), Token);

        Assert.Equal(["DefaultBranch"], forge.Calls);
    }

    [Fact]
    public async Task A_pr_fact_given_as_stored_json_text_is_read()
    {
        var stored = JsonSerializer.Serialize(PrOnBase("main"));

        var result = await new PrBaseIsDefaultGate(Forge()).Evaluate(Of(stored), Token);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Fact]
    public async Task A_bare_link_takes_its_base_from_the_snapshot()
    {
        var result = await new PrBaseIsDefaultGate(Forge()).Evaluate(Of($"\"{Link}\"", prState: """{"base":{"ref":"main"}}"""), Token);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Fact]
    public async Task No_base_ref_anywhere_fails_closed()
    {
        var result = await new PrBaseIsDefaultGate(Forge()).Evaluate(Of($"\"{Link}\""), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("\"not a link\"")]
    [InlineData("\"https://github.com/acme/app/issues/7\"")]
    public async Task A_missing_or_unreadable_link_fails_closed_without_asking_the_forge(string? pr)
    {
        var forge = Forge();

        var result = await new PrBaseIsDefaultGate(forge).Evaluate(Of(pr, prState: """{"base":{"ref":"main"}}"""), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Empty(forge.Calls);
    }

    [Fact]
    public async Task A_forge_that_refuses_fails_closed_with_its_reason()
    {
        var result = await new PrBaseIsDefaultGate(new FakePullRequests(["gitlab.example"])).Evaluate(Of(PrOnBase("main")), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains("github.com", Assert.Single(result.Reasons), StringComparison.Ordinal);
    }
}
