using System.Text.Json.Nodes;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Host.Commands.Scoping;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.check' on the block runtime keeps the round history (scoping spec §8.3, §9): one line per run in the instance's run area, which the desk's evidence step packs.</summary>
public sealed class ScopeCheckRoundsTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<BlockStepResult> Check(IRunArea area, ScopingFacts facts) =>
        new ScopeCheckStep(area).Run(ScopingStepContexts.Of("step.scope.check", facts), Token).AsTask();

    private static JsonNode[] Lines(string text) => [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => JsonNode.Parse(l)!)];

    [Fact, Trait("Lane", "unit")]
    public async Task Two_check_runs_leave_two_numbered_round_lines()
    {
        var area = new InMemoryRunArea();

        _ = await Check(area, ScopingFacts.Expiry().Without("rounds"));
        _ = await Check(area, ScopingFacts.Expiry().Edit("acceptance", n => n["items"]!.AsArray().RemoveAt(0)).Without("rounds"));

        var lines = Lines((await area.Read(ScopingStepContexts.Address, "rounds.jsonl", Token)).Value);
        Assert.Equal([1, 2], lines.Select(static l => l["round"]!.GetValue<int>()));
        Assert.Equal(["first", "revise"], lines.Select(static l => l["trigger"]!.GetValue<string>()), StringComparer.Ordinal);
        _ = Assert.Single(lines[1]["removed"]!.AsArray());   // diffed against the first line's digest
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_area_that_cannot_be_written_fails_the_Step()
    {
        var result = await Check(new InMemoryRunArea { Unwritable = true }, ScopingFacts.Expiry().Without("rounds"));

        Assert.Null(result.Result);
        Assert.Contains("cannot be written", result.Failure, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Round_lines_on_disk_reach_the_desk_evidence_packet()
    {
        var area = new DiskRunArea(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")));
        _ = await Check(area, ScopingFacts.Expiry().Without("rounds"));
        _ = await Check(area, ScopingFacts.Expiry().Without("rounds"));

        var directory = RunAreaDirectory.Of(_home.Path, WorkflowInstanceId.Of(InstanceAddress.From(ScopingStepContexts.Address)));
        var facts = new Dictionary<string, string>(ScopingFacts.Expiry().Without("rounds"), StringComparer.Ordinal);
        await ScopingFactDirectory.OverlayRounds(facts, directory, Token);
        var packet = ScopePacket.Build(new ScopeFacts(facts));

        var rounds = Assert.Single(packet, f => f.Path.EndsWith("rounds.jsonl", StringComparison.Ordinal));
        Assert.Equal(2, Lines(rounds.Content).Length);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_home_that_is_a_file_fails_the_write_with_SND_BLOCK_004()
    {
        var file = Path.Combine(_home.Path, "not-a-directory");
        await File.WriteAllTextAsync(file, "x", Token);

        var written = await new DiskRunArea(new SindriSettings(file, Path.Combine(_home.Path, "profile.yaml"))).Write(ScopingStepContexts.Address, "rounds.jsonl", "{}\n", append: true, Token);

        Assert.Equal("SND-BLOCK-004", written.Error.Code);
    }
}
