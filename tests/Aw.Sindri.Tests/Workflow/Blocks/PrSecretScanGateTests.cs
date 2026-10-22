using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Blocks.PrGateTestInputs;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>'gate.pr.secret-scan' (PB spec §10.1): the push's added lines go through the shared secret patterns; a hit refuses, naming file and line only.</summary>
public sealed class PrSecretScanGateTests
{
    private static readonly string _token = "TOKEN" + "-" + new string('x', 24);

    [Fact, Trait("Lane", "unit")]
    public async Task A_push_without_a_secret_passes()
    {
        var result = await Evaluate(new FakeDiffContent().Adding(Workspace, "src/App.cs", "var x = 1;", "return x;"));

        Assert.Equal(GateVerdict.Pass, result.Verdict);
        Assert.Empty(result.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_secret_on_an_added_line_refuses_naming_file_and_line_never_the_match()
    {
        var result = await Evaluate(new FakeDiffContent().Adding(Workspace, "src/App.cs", "var x = 1;", $"var t = \"{_token}\";"));

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        var reason = Assert.Single(result.Reasons);
        Assert.Contains("src/App.cs:2", reason, StringComparison.Ordinal);
        Assert.DoesNotContain(_token, reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_scan_starts_at_the_head_the_snapshot_saw()
    {
        var content = new FakeDiffContent().Add(Workspace);

        _ = await Evaluate(content, PrState(headSha: "h7"));

        Assert.Equal([$"{Workspace} h7"], content.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_workspace_refuses_rather_than_passing_unscanned()
    {
        var gate = new PrSecretScanGate(new FakeDiffContent(), new MarkingScrubber());

        var result = await gate.Evaluate(GateOf("gate.pr.secret-scan", Arguments(("since", PrState()))), TestContext.Current.CancellationToken);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_worktree_git_cannot_read_refuses()
    {
        var result = await Evaluate(new FakeDiffContent());

        Assert.Equal(GateVerdict.Fail, result.Verdict);
    }

    private static async Task<GateResult> Evaluate(FakeDiffContent content, string? prState = null) =>
        await new PrSecretScanGate(content, new MarkingScrubber()).Evaluate(
            GateOf("gate.pr.secret-scan", Arguments(("workspace", $"\"{Workspace}\""), ("since", prState ?? PrState()))), TestContext.Current.CancellationToken);

    /// <summary>Changes any line that holds the test token, as the shared scrubber changes a secret.</summary>
    private sealed class MarkingScrubber : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(_token, "[REDACTED:test]", StringComparison.Ordinal);
    }
}
