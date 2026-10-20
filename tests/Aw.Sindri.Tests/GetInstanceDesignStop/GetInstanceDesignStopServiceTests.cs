using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Line;
using Aw.Sindri.Tests.Design;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.GetInstanceDesignStop;

/// <summary>'GetInstanceDesignStop' (line spec LN-API2): the stop-2 view over a workflow.plan instance's facts, scoped like every instance read.</summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceDesignStopServiceTests
{
    private const string Item = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private sealed class Contents(IReadOnlyDictionary<string, string> bySha) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(bySha[fact.Sha256])));
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal);
    }

    /// <summary>The instance's facts as the plan station's Steps write them: fact:design as schemas/line/design.json has it (sections, coverage, decisions, signals and contracts as arrays), fact:plan the task plan, fact:holdoutSets the hidden sets by hash, fact:lensRounds the lens log.</summary>
    internal static Dictionary<string, string> Facts(LineFixture gold)
    {
        var lensRounds = new JsonArray([.. gold["design/lens-rounds.jsonl"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => JsonNode.Parse(l))]);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["design"] = DesignFact(gold),
            ["plan"] = gold["tasks.json"],
            ["holdoutSets"] = gold["holdout.json"],
            ["lensRounds"] = lensRounds.ToJsonString(),
        };
    }

    private static string DesignFact(LineFixture gold)
    {
        var sections = ((JsonArray)JsonNode.Parse(gold["design/sections.json"])!["sections"]!).Select(static s => (JsonNode)new JsonObject
        {
            ["id"] = (string?)s!["id"],
            ["title"] = (string?)s["heading"],
            ["covers"] = s["covers"]!.DeepClone(),
            ["cites"] = s["cites"]!.DeepClone(),
            ["assumes"] = s["assumes"]!.AsArray().Count > 0 ? (string?)s["assumes"]![0] : null,
        }).ToArray();
        var coverage = JsonNode.Parse(gold["design/coverage.json"])!;
        var rows = coverage["surfaces"]!.AsArray().Select(static c => (JsonNode)new JsonObject { ["surface"] = (string?)c!["id"], ["sections"] = c["sections"]!.DeepClone() })
            .Concat(coverage["excluded"]!.AsArray().Select(static c => (JsonNode)new JsonObject { ["surface"] = (string?)c!["id"], ["excluded"] = (string?)c["reason"] })).ToArray();
        var decisions = ((JsonArray)JsonNode.Parse(gold["design/decisions.json"])!["decisions"]!).Select(static d => { var copy = (JsonObject)d!.DeepClone(); _ = copy.Remove("chosen"); return (JsonNode)copy; }).ToArray();
        var signals = JsonNode.Parse(gold["design/signals.json"])!["signals"]!.AsObject().Select(static p => (JsonNode)new JsonObject
        {
            ["item"] = p.Key,
            ["signals"] = new JsonArray(p.Value!.AsArray().Select(static s => (JsonNode)new JsonObject { ["kind"] = (string?)s!["kind"], ["statement"] = (string?)s["statement"], ["runs"] = (string?)s["runsAt"] }).ToArray()),
        }).ToArray();
        var contracts = ((JsonArray)JsonNode.Parse(gold["design/contracts.json"])!["edits"]!).Select(static e => (JsonNode)new JsonObject
        {
            ["file"] = (string?)e!["target"]!["file"],
            ["heading"] = (string?)e["target"]!["heading"],
            ["quote"] = (string?)e["quote"],
            ["edit"] = (string?)e["change"],
            ["row"] = (int)e["row"]!,
            ["lane"] = (string?)e["lane"],
        }).ToArray();
        return new JsonObject
        {
            ["sections"] = new JsonArray(sections),
            ["coverage"] = new JsonArray(rows),
            ["decisions"] = new JsonArray(decisions),
            ["signals"] = new JsonArray(signals),
            ["contracts"] = new JsonArray(contracts),
        }.ToJsonString();
    }

    private static (InstanceView View, Contents Contents) Instance(string recipe, Dictionary<string, string> facts)
    {
        var view = StatusCases.View("desk-task");
        var shas = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var (key, body) in facts)
        {
            var sha = (++n).ToString("x64", CultureInfo.InvariantCulture);
            shas["fact:" + key] = sha;
            text[sha] = body;
        }

        return (view with { ItemId = ItemKey.From(Item), Recipe = view.Recipe with { Id = RecipeId.From(recipe) }, FactShas = shas }, new Contents(text));
    }

    private static async Task<Result<DesignStopView>> Read(string recipe, Dictionary<string, string> facts, CallerContext? caller = null, FixedRepos? repos = null)
    {
        var (view, contents) = Instance(recipe, facts);
        var instances = new SomeInstances(view);
        var service = new GetInstanceDesignStopService(new PlanInstances(instances, instances, repos ?? new FixedRepos(), new ListInstances.FixedCaller(caller ?? new CallerContextBuilder().Human().Build())), contents, new Redacting());
        return await service.Handle(new GetInstanceDesignStopRequest(view.Id), CancellationToken.None);
    }

    [Fact]
    public async Task A_plan_instances_facts_become_the_view_the_files_give_where_the_facts_hold_them()
    {
        var gold = LineFixture.Plan();

        var result = await Read("workflow.plan", Facts(gold));
        var files = await DesignStopViewTests.Show(gold);

        Assert.True(result.IsSuccess);
        Assert.Equal(["models", "acceptance", "escapes", "spec"], result.Value.NotYet, StringComparer.Ordinal);   // no instance fact holds them yet
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(files.Graph), System.Text.Json.JsonSerializer.Serialize(result.Value.Graph));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(files.LensRounds), System.Text.Json.JsonSerializer.Serialize(result.Value.LensRounds));
        Assert.Equal(files.Decisions.Select(static d => (d.Decision, d.Recommended, d.Options.Count)), result.Value.Decisions.Select(static d => (d.Decision, d.Recommended, d.Options.Count)));
        Assert.Equal(files.Trace.Select(static t => (t.Item, t.Phase, t.Signals.Count)), result.Value.Trace.Select(static t => (t.Item, t.Phase, t.Signals.Count)));
        Assert.Equal(files.Coverage.Select(static c => (c.Scope, string.Join(',', c.Sections), c.Excluded)), result.Value.Coverage.Select(static c => (c.Scope, string.Join(',', c.Sections), c.Excluded)));
        Assert.Equal("D3.a", result.Value.Spec.Assumes.Single(static a => a.Section == "sec-2.2").Option);
        Assert.Equal(("X1", 1, "docs"), (result.Value.Contracts[0].Edit, result.Value.Contracts[0].Row, result.Value.Contracts[0].Lane));
    }

    [Fact]
    public async Task The_designs_example_in_schemas_line_reads_into_the_view()
    {
        var decision = await Read("workflow.plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["design"] = LineFixture.DesignExample("design.decision") });
        var minimal = await Read("workflow.plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["design"] = LineFixture.DesignExample("design.minimal") });

        var d3 = Assert.Single(decision.Value.Decisions);
        Assert.Equal(("D3", "open", "D3.a", null), (d3.Decision, d3.Status, d3.Recommended, d3.Chosen));
        Assert.Equal(["sec-4.2", "sec-6"], d3.Affects, StringComparer.Ordinal);
        Assert.Contains(new StopAssumes("sec-4.2", "D3", "D3.a"), decision.Value.Spec.Assumes);
        Assert.Contains(minimal.Value.NotYet, n => n == "decisions");
        Assert.Empty(minimal.Value.Spec.Assumes);
    }

    [Fact]
    public async Task The_view_is_live_before_evidence_with_what_the_instance_lacks_named_in_notYet()
    {
        var result = await Read("workflow.plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["plan"] = LineFixture.Plan()["tasks.json"] });

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.NotYet, s => s == "decisions");
        Assert.Contains(result.Value.NotYet, s => s == "lensRounds");
        Assert.NotEmpty(result.Value.Graph.Phases);
    }

    [Fact]
    public async Task A_freeze_refusal_the_instance_holds_is_the_banner()
    {
        var facts = Facts(LineFixture.Plan());
        facts["freezeRefused"] = "{\"rule\":\"SND-LINE-001\",\"message\":\"the design packet changed after the approve\"}";

        var result = await Read("workflow.plan", facts);

        Assert.Equal("SND-LINE-001", result.Value.FreezeRefused!.Rule);
    }

    [Fact]
    public async Task An_instance_of_another_recipe_is_SND_LINE_003()
    {
        var result = await Read("workflow.design", Facts(LineFixture.Plan()));

        Assert.Equal(LineErrors.WrongRecipe.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_session_of_another_repo_gets_item_404_and_one_of_the_same_repo_reads()
    {
        var repos = new FixedRepos((Item, "alpha"));

        var own = await Read("workflow.plan", Facts(LineFixture.Plan()), new CallerContextBuilder().Session().InRepo("alpha").Build(), repos);
        var other = await Read("workflow.plan", Facts(LineFixture.Plan()), new CallerContextBuilder().Session().InRepo("beta").Build(), repos);

        Assert.True(own.IsSuccess);
        Assert.Equal(SindriErrors.ItemNotFound.Code, other.Error.Code);
    }

    [Fact]
    public async Task Secrets_in_a_fact_are_scrubbed_before_the_view_is_built()
    {
        var gold = LineFixture.Plan().EditText("design/decisions.json", t => t.Replace("Which expiry choices", "Which sk-live-123 choices", StringComparison.Ordinal));

        var result = await Read("workflow.plan", Facts(gold));

        Assert.DoesNotContain("sk-live-123", result.Value.Decisions[0].Question, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result.Value.Decisions[0].Question, StringComparison.Ordinal);
    }
}
