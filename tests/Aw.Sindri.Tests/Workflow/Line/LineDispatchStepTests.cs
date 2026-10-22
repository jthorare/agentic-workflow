using System.Text.Json.Nodes;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>'step.line.seed' and 'step.line.dispatch' (line spec §8.2, LN-WF1) over their facts: the seeded board, and the decision a board and an event give.</summary>
public sealed class LineDispatchStepTests
{
    private const string Packets = """[{"phase":"a","id":"a","hash":"sha256:a","dir":"p/a","deps":[]},{"phase":"b","id":"b","hash":"sha256:b","dir":"p/b","deps":["a"]},{"phase":"c","id":"c","hash":"sha256:c","dir":"p/c","deps":[]}]""";

    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Fact(BlockStepResult result, string key) => result.Result!.Outputs[FactKey.From(key)].GetRawText();

    private static async Task<string> SeededBoard()
    {
        var seeded = await new LineSeedStep().Run(BuildShipContexts.Of("step.line.seed", new Dictionary<string, string>(StringComparer.Ordinal) { ["phasePackets"] = Packets }), Token);
        return Fact(seeded, "fact:phaseBoard");
    }

    private Task<BlockStepResult> Dispatch(string board, string phaseEvent = "{}", string edit = "{}") => new LineDispatchStep(_profiles).Run(
        BuildShipContexts.Of("step.line.dispatch", new Dictionary<string, string>(StringComparer.Ordinal) { ["phaseBoard"] = board, ["phaseEvent"] = phaseEvent, ["boardEdit"] = edit }), Token).AsTask();

    [Fact, Trait("Lane", "unit")]
    public async Task Seed_writes_a_waiting_board_and_the_empty_event_and_edit()
    {
        var result = await new LineSeedStep().Run(BuildShipContexts.Of("step.line.seed", new Dictionary<string, string>(StringComparer.Ordinal) { ["phasePackets"] = Packets }), Token);

        Assert.Equal("seeded", result.Result?.Outcome.Value);
        Assert.Equal(["a", "b", "c"], JsonNode.Parse(Fact(result, "fact:phaseBoard"))!["phases"]!.AsArray().Select(p => p!["phase"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("{}", Fact(result, "fact:phaseEvent"));
        Assert.Equal("{}", Fact(result, "fact:boardEdit"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Seed_fails_a_plan_with_no_phase()
    {
        var result = await new LineSeedStep().Run(BuildShipContexts.Of("step.line.seed", new Dictionary<string, string>(StringComparer.Ordinal) { ["phasePackets"] = "[]" }), Token);

        Assert.Null(result.Result);
        Assert.Contains("no phase", result.Failure, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dispatch_starts_the_first_ready_phase_and_marks_it_running()
    {
        var result = await Dispatch(await SeededBoard());

        Assert.Equal("start", result.Result?.Outcome.Value);
        Assert.Equal("a", JsonNode.Parse(Fact(result, "fact:nextPhase"))!["phase"]!.GetValue<string>());
        Assert.Equal("running", JsonNode.Parse(Fact(result, "fact:phaseBoard"))!["phases"]![0]!["state"]!.GetValue<string>());
        Assert.Equal("{}", Fact(result, "fact:phaseEvent"));
        Assert.Equal("{}", Fact(result, "fact:boardEdit"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dispatch_stops_at_the_profiles_parallel_cap()
    {
        _ = await _profiles.Save(new LoadedProfile(new SindriProfile(1, "dev", Workflow: new WorkflowSettings(Line: new LineSettings(Parallel: 1))), ProfileHash.From(new string('b', 64)), "/p.yaml"), Token);
        var first = await Dispatch(await SeededBoard());

        var second = await Dispatch(Fact(first, "fact:phaseBoard"));

        Assert.Equal("full", second.Result?.Outcome.Value);
        Assert.False(second.Result!.Outputs.ContainsKey(FactKey.From("fact:nextPhase")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dispatch_folds_the_event_then_stacks_the_next_phase_on_the_shipped_one()
    {
        var first = await Dispatch(await SeededBoard());
        var second = await Dispatch(Fact(first, "fact:phaseBoard"));   // c, the other independent phase
        var shipped = """{"phase":"a","attempt":1,"outcome":"shipped","headBranch":"sindri/a"}""";

        var third = await Dispatch(Fact(second, "fact:phaseBoard"), shipped);

        var next = JsonNode.Parse(Fact(third, "fact:nextPhase"))!;
        Assert.Equal("start", third.Result?.Outcome.Value);
        Assert.Equal(("b", "a", "sindri/a"), (next["phase"]!.GetValue<string>(), next["stackParent"]!.GetValue<string>(), next["base"]!["ref"]!.GetValue<string>()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dispatch_reports_blocked_when_a_phase_ended_without_merging_and_done_once_every_phase_is_settled()
    {
        var first = await Dispatch(await SeededBoard());
        var failed = await Dispatch(Fact(first, "fact:phaseBoard"), """{"phase":"a","attempt":1,"outcome":"ended","ended":"unresolved"}""");
        var skipped = await Dispatch(Fact(failed, "fact:phaseBoard"), edit: """{"edit":"skip","phase":"a"}""");
        var rest = await Dispatch(Fact(skipped, "fact:phaseBoard"));   // c starts; b was skipped with a
        var merged = await Dispatch(Fact(rest, "fact:phaseBoard"), """{"phase":"c","attempt":1,"outcome":"merged"}""");

        Assert.Equal(["blocked", "start", "done"], new[] { failed, skipped, merged }.Select(static r => r.Result!.Outcome.Value), StringComparer.Ordinal);
        Assert.Equal("c", JsonNode.Parse(Fact(skipped, "fact:nextPhase"))!["phase"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dispatch_fails_without_a_board()
    {
        var result = await Dispatch("{}");

        Assert.Null(result.Result);
        Assert.Contains("fact:phaseBoard", result.Failure, StringComparison.Ordinal);
    }
}
