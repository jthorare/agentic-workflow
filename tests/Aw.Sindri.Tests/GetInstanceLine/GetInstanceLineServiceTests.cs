using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.GetInstanceLine;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Tests.GetInstance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstanceLine;

/// <summary>'GetInstanceLine' (line spec LN-API1): the line board over a workflow.line instance's facts and its linked phase instances, scoped like every instance read.</summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceLineServiceTests
{
    private const string Item = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Line = "T-3/line/1";
    private static readonly string _scopeHash = "sha256:" + new string('1', 64);
    private static readonly string _designHash = "sha256:" + new string('2', 64);
    private static readonly DateTimeOffset _now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly ConcurrentDictionary<string, string> _stored = new(StringComparer.Ordinal);
    private static int _facts;

    private sealed class Contents : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(_stored[fact.Sha256])));
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal);
    }

    private sealed class NoProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LoadedProfile?>(value: null));
    }

    private sealed class Facts
    {
        internal Dictionary<string, string> Shas { get; } = new(StringComparer.Ordinal);

        internal IFactContents Contents { get; } = new GetInstanceLineServiceTests.Contents();

        internal Facts Add(string key, string json)
        {
            var sha = Interlocked.Increment(ref _facts).ToString("x64", CultureInfo.InvariantCulture);
            Shas["fact:" + key] = sha;
            _stored[sha] = json;
            return this;
        }
    }

    private static InstanceView Instance(string address, string recipe, Facts facts, int tokens = 0, int startedMinutesAgo = 0, string next = "")
    {
        var view = StatusCases.View("desk-task");
        return view with
        {
            Address = InstanceAddress.From(address),
            Id = WorkflowInstanceId.Of(InstanceAddress.From(address)),
            ItemId = ItemKey.From(Item),
            Recipe = view.Recipe with { Id = RecipeId.From(recipe) },
            FactShas = facts.Shas,
            Next = next,
            Budget = new BudgetView(State: 0, StateCap: null, Instance: tokens, InstanceCap: null),
            StartedAt = _now.AddMinutes(-startedMinutesAgo),
            Outcome = null,
        };
    }

    private static string Board(params (string Phase, string State, int Attempt, string? Park)[] rows) => new JsonObject
    {
        ["phases"] = new JsonArray([.. rows.Select(static r => (JsonNode)new JsonObject
        {
            ["phase"] = r.Phase,
            ["entry"] = new JsonObject { ["phase"] = r.Phase },
            ["deps"] = new JsonArray(),
            ["state"] = r.State,
            ["attempt"] = r.Attempt,
            ["parked"] = r.Park is not null,
            ["parkReason"] = r.Park,
        })]),
    }.ToJsonString();

    private static async Task<Result<LineView>> Read(IEnumerable<InstanceView> views, string line, IFactContents contents, IReadOnlyList<FrozenPacket>? held = null)
    {
        var all = views.ToArray();
        var instances = new SomeInstances(all);
        var service = new GetInstanceLineService(
            new LineInstances(instances, instances),
            instances,
            contents,
            new Redacting(),
            new InMemoryFrozenPackets(held ?? []),
            new NoProfile(),
            new FakeTimeProvider(_now));
        return await service.Handle(new GetInstanceLineRequest(all.First(v => v.Address.Value == line).Id), CancellationToken.None);
    }

    internal static async Task<LineView> Gold()
    {
        var lineFacts = new Facts()
            .Add("scopePacket", new JsonObject { ["hash"] = _scopeHash, ["dir"] = "/runs/scope" }.ToJsonString())
            .Add("designPacket", JsonValue.Create(_designHash).ToJsonString())
            .Add("phaseBoard", Board(("p1", "running", 1, null), ("p2", "waiting", 0, null), ("p3", "failed", 2, "the build ran out of budget")));
        var line = Instance(Line, "workflow.line", lineFacts) with { Children = [new ChildView(InstanceAddress.From(Line + "/plan.4"), "wait")] };
        var p1Facts = new Facts()
            .Add("phasePacket", new JsonObject { ["base"] = new JsonObject { ["rule"] = "dependency-head", ["ref"] = "sindri/expiry/p0" } }.ToJsonString())
            .Add("workspace", new JsonObject { ["path"] = "/work/expiry/p1" }.ToJsonString())
            .Add("pr", JsonValue.Create("https://github.com/o/r/pull/7").ToJsonString());
        var p1 = Instance(Line + "/phase.p1.1", "workflow.phase", p1Facts, tokens: 1234, startedMinutesAgo: 90, next: "Waiting for the build.");
        var p3 = Instance(Line + "/phase.p3.2", "workflow.phase", new Facts(), tokens: 50, startedMinutesAgo: 10, next: "Parked.");
        return (await Read([line, p1, p3], Line, lineFacts.Contents, [new FrozenPacket(_designHash, "design-expiry", "design", PacketFreezer.Owner)])).Value;
    }

    [Fact]
    public async Task A_line_instance_shows_the_packet_chain_with_what_is_frozen_and_its_phases_in_plan_order()
    {
        var view = await Gold();

        Assert.Equal(["scoping", "design", "plan"], view.Stations.Select(s => s.Station), StringComparer.Ordinal);
        Assert.Equal(new LinePacket("scoping", _scopeHash, Frozen: false), view.Stations[0].Packet);
        Assert.Equal(new LinePacket("design", _designHash, Frozen: true), view.Stations[1].Packet);
        Assert.Null(view.Stations[2].Packet);
        Assert.Equal(Line + "/plan.4", view.Stations[2].Instance);
        Assert.Equal(["p1", "p2", "p3"], view.Phases.Select(p => p.Id.Value), StringComparer.Ordinal);
        Assert.Equal((2, 4), (view.Parallel, view.MaxOpenPrs));
    }

    [Fact]
    public async Task A_running_phase_joins_its_linked_instance_for_pr_base_worktree_tokens_and_wall_clock()
    {
        var view = await Gold();

        var p1 = view.Phases[0];
        Assert.Equal(new LinePhaseView(LinePhaseId.From("p1"), Attempt: 1, Line + "/phase.p1.1", "running", ParkReason: null, "https://github.com/o/r/pull/7", "sindri/expiry/p0", "/work/expiry/p1", "Waiting for the build.", Tokens: 1234, WallMinutes: 90), p1);
    }

    [Fact]
    public async Task A_phase_that_has_not_started_has_no_instance_and_a_parked_one_names_its_reason()
    {
        var view = await Gold();

        Assert.Equal(new LinePhaseView(LinePhaseId.From("p2"), Attempt: 0, Address: null, "waiting", ParkReason: null, Pr: null, Base: null, Worktree: null, string.Empty, Tokens: 0, WallMinutes: 0), view.Phases[1]);
        Assert.Equal(("failed", "the build ran out of budget", Line + "/phase.p3.2"), (view.Phases[2].State, view.Phases[2].ParkReason, view.Phases[2].Address));
    }

    [Fact]
    public async Task A_started_phase_whose_instance_cannot_be_read_still_shows_its_board_row()
    {
        var facts = new Facts().Add("phaseBoard", Board(("p1", "running", 1, null)));
        var line = Instance(Line, "workflow.line", facts);

        var result = await Read([line], Line, facts.Contents);

        Assert.Equal(new LinePhaseView(LinePhaseId.From("p1"), Attempt: 1, Line + "/phase.p1.1", "running", ParkReason: null, Pr: null, Base: null, Worktree: null, string.Empty, Tokens: 0, WallMinutes: 0), result.Value.Phases[0]);
    }

    [Fact]
    public async Task A_line_with_no_facts_yet_has_three_empty_stations_and_no_phases()
    {
        var facts = new Facts();

        var result = await Read([Instance(Line, "workflow.line", facts)], Line, facts.Contents);

        Assert.All(result.Value.Stations, static s => Assert.Null(s.Packet));
        Assert.Empty(result.Value.Phases);
    }

    [Fact]
    public async Task Another_recipes_instance_is_SND_LINE_003()
    {
        var facts = new Facts();
        var plan = Instance("T-3/plan/1", "workflow.plan", facts);

        var result = await Read([plan], "T-3/plan/1", facts.Contents);

        Assert.Equal(LineErrors.WrongRecipe.Code, result.Error.Code);
    }

    [Fact]
    public async Task Text_comes_out_scrubbed()
    {
        var lineFacts = new Facts().Add("phaseBoard", Board(("p1", "failed", 1, "token sk-live-123 was rejected")));
        var line = Instance(Line, "workflow.line", lineFacts);

        var result = await Read([line], Line, lineFacts.Contents);

        Assert.Equal("token [redacted] was rejected", result.Value.Phases[0].ParkReason);
    }

    [Fact]
    public async Task An_unknown_instance_is_item_404()
    {
        var facts = new Facts();
        var line = Instance(Line, "workflow.line", facts);
        var instances = new SomeInstances(line);
        var service = new GetInstanceLineService(new LineInstances(instances, instances), instances, facts.Contents, new Redacting(), new InMemoryFrozenPackets([]), new NoProfile(), new FakeTimeProvider(_now));

        var result = await service.Handle(new GetInstanceLineRequest(WorkflowInstanceId.Of(InstanceAddress.From("T-9/line/1"))), CancellationToken.None);

        Assert.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task The_gold_line_matches_its_golden()
    {
        var view = await Gold();

        Golden.Match(JsonSerializer.Serialize(view, _indented) + "\n", typeof(GetInstanceLineServiceTests).Assembly, "Golden/Api/workflow-instance-line/gold-line.json", Normalization.None);
    }

    private static readonly JsonSerializerOptions _indented = new(AwJson.CreateOptions(SindriJsonContext.Default)) { WriteIndented = true };
}
