using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The workflow view routes as the running host serves them (API spec section 3.8, API P11): a served instance, list, fact
/// and recipe read back into the section 3.8 records, so the bodies the UI builds on are the ones the routes write.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ServedWorkflowRoutesTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    /// <summary>The goldens this class records from the served routes (the rest of 'Golden/Api/workflow-*' is the fixtures' in <see cref="WorkflowViewGoldenTests"/>).</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Goldens { get; } =
    [
        ("workflow-instance", "not-found"), ("workflow-instance-fact", "not-found"), ("workflow-recipes", "list"),
        ("workflow-instances-start", "request"), ("workflow-instances-start", "started"), ("workflow-instances", "start-item-malformed"), ("workflow-instances", "start-missing-input"),
        ("workflow-instances", "start-item-mismatch"), ("workflow-instances", "start-earlier-attempt-live"),
        ("workflow-instances", "start-recipe-legacy"), ("workflow-instances", "start-recipe-revoked"), ("workflow-instances", "start-recipe-drifted"),
        ("workflow-instance-scoping", "expiry-stop"),   // recorded by ScopingShowTests (API2 serves that view as is)
        ("workflow-instance-design-stop", "gold-stop"),   // recorded by DesignStopViewTests (LN-API2 serves that view as is)
    ];

    /// <summary>What a served start draws at run time: its timestamps and the instance id.</summary>
    private static readonly NormalizationRule[] _drawn = [Normalization.IsoTimestamps, new("instanceId", "WFI-[0-9a-f]{32}", "WFI-<id>")];

    private const string Instances = "/sindri/workflow/instances";

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Get_instance_serves_InstanceDetail_that_reads_back_into_the_record()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var id = await Started(served, "T-1");

        var body = await Read(served, "/sindri/workflow/instances/" + id);

        var options = served.Sindri.GetRequiredService<JsonSerializerOptions>();
        var detail = JsonSerializer.Deserialize<InstanceDetail>(body, options)!;
        Assert.Equal("T-1/step/1", detail.Address.Value);
        Assert.NotEmpty(detail.Current.Status.Text);
        Assert.Equal(ApiGolden.Reindent(body), ApiGolden.Reindent(JsonSerializer.Serialize(detail, options)));
    }

    [Fact(Timeout = 120_000)]
    public async Task List_instances_serves_InstanceList_with_counts_over_the_root_set()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        _ = await Started(served, "T-1");
        _ = await Started(served, "T-2");
        _ = await served.Cli("cancel", "T-2/step/1");
        _ = await served.Until("T-2/step/1", static v => v.State == "cancelled");

        var list = await ListUntil(served, "/sindri/workflow/instances", static l => l.Instances.Count == 2);

        Assert.Equal(new ListCounts(Open: 1, NeedsYou: 1, WithYou: 0, Running: 0, Done: 1), list.Counts);
        Assert.Null(list.Empty);
    }

    [Fact(Timeout = 120_000)]
    public async Task List_instances_open_true_keeps_only_unfinished_rows()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        _ = await Started(served, "T-1");
        _ = await Started(served, "T-2");
        _ = await served.Cli("cancel", "T-2/step/1");
        _ = await served.Until("T-2/step/1", static v => v.State == "cancelled");

        var list = await ListUntil(served, "/sindri/workflow/instances?open=true", static l => l.Instances.Count == 1);

        Assert.Equal(["T-1/step/1"], list.Instances.Select(i => i.Address.Value), StringComparer.Ordinal);
        Assert.Equal(1, list.Counts.Done);   // the counts cover the root set before the open filter
    }

    [Fact(Timeout = 120_000)]
    public async Task Get_instance_fact_serves_FactView_and_an_undeclared_key_is_SND_ITEM_404()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var id = await Started(served, "T-1");

        var fact = await Read(served, $"/sindri/workflow/instances/{id}/facts/{Uri.EscapeDataString("fact:spec")}");
        var missing = await served.Http.Ask(Get($"/sindri/workflow/instances/{id}/facts/{Uri.EscapeDataString("fact:unknown")}", Human(Token(served))));

        var view = JsonSerializer.Deserialize<FactView>(fact, served.Sindri.GetRequiredService<JsonSerializerOptions>())!;
        Assert.Equal(("fact:spec", FactType.Text, "the spec", false), (view.Key, view.Type, view.Body.GetString(), view.Truncated));
        Assert.Equal((HttpStatusCode.NotFound, "SND-ITEM-404"), (missing.Status, missing.Code));
    }

    [Fact(Timeout = 120_000)]
    public async Task An_unknown_instance_answers_the_pinned_not_found_body()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);

        var unknown = await served.Http.Ask(Get("/sindri/workflow/instances/WFI-" + new string('a', 32), Human(Token(served))));

        Assert.Equal((HttpStatusCode.NotFound, "SND-ITEM-404"), (unknown.Status, unknown.Code));
        ApiGolden.Match(unknown.Body, _harness.Home, "workflow-instance", "not-found");
    }

    [Fact(Timeout = 120_000)]
    public async Task An_undeclared_fact_answers_the_pinned_not_found_body()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var id = await Started(served, "T-1");

        var undeclared = await served.Http.Ask(Get($"/sindri/workflow/instances/{id}/facts/{Uri.EscapeDataString("fact:unknown")}", Human(Token(served))));

        Assert.Equal((HttpStatusCode.NotFound, "SND-ITEM-404"), (undeclared.Status, undeclared.Code));
        ApiGolden.Match(undeclared.Body, _harness.Home, "workflow-instance-fact", "not-found");
    }

    [Fact(Timeout = 120_000)]
    public async Task Get_recipe_by_id_serves_RecipeView_with_outputs_and_every_input_kind()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/desk-round-trip.yaml"));
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);

        var body = await Read(served, "/sindri/workflow/recipes/workflow.dev-desk-round-trip");

        var view = JsonSerializer.Deserialize<RecipeView>(body, served.Sindri.GetRequiredService<JsonSerializerOptions>())!;
        Assert.Equal("workflow.dev-desk-round-trip", view.Id.Value);
        Assert.Contains(view.Outputs, o => o.Key == "fact:output");
        Assert.All(view.Inputs, i => Assert.False(string.IsNullOrEmpty(i.Kind)));
    }

    [Fact(Timeout = 120_000)]
    public async Task Get_recipe_by_id_at_hash_serves_the_same_body()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var byId = await Read(served, "/sindri/workflow/recipes/workflow.step");
        var hash = JsonDocument.Parse(byId).RootElement.GetProperty("hash").GetString()!;

        var byHash = await Read(served, "/sindri/workflow/recipes/workflow.step@" + hash.Replace("sha256:", string.Empty, StringComparison.Ordinal));

        Assert.Equal(byId, byHash);
    }

    [Fact(Timeout = 120_000)]
    public async Task Dev_triage_and_desk_round_trip_serve_the_golden_shape()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/desk-round-trip.yaml"));
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/triage-three-state.yaml"));
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        foreach (var (recipe, item, golden, facts) in new[]
        {
            ("workflow.dev-desk-round-trip", "T-3", "dev-desk-round-trip", new[] { "fact:request" }),
            ("workflow.dev-triage-three-state", "T-4", "dev-triage-three-state", ["fact:scope", "fact:item"]),
        })
        {
            var started = await served.Cli(["start", recipe, "--item", item, .. facts.SelectMany(f => new[] { "--fact", f + "=" + _harness.File(item + f[5..] + ".md", f == "fact:item" ? $"{{ \"id\": \"{item}\", \"repo\": \"app\", \"spec\": \"the spec\" }}" : "text of " + f) })]);
            Assert.True(started.Exit == 0, started.Stderr);
            var id = await IdOfItem(served, item);

            var served1 = JsonDocument.Parse(await Read(served, "/sindri/workflow/instances/" + id)).RootElement;

            var expected = JsonDocument.Parse(await File.ReadAllTextAsync(GoldenPath(golden), TestContext.Current.CancellationToken)).RootElement;
            Assert.Equal(Keys(expected), Keys(served1));
            Assert.Equal(Cells(expected), Cells(served1));
            Assert.Equal(FactStatuses(expected), FactStatuses(served1));
        }
    }

    [Fact(Timeout = 120_000)]
    public async Task List_recipes_serves_the_registered_recipes()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);

        var body = await Read(served, "/sindri/workflow/recipes");

        ApiGolden.Match(body, _harness.Home, "workflow-recipes", "list", _drawn);
    }

    [Fact(Timeout = 120_000)]
    public async Task Start_serves_the_started_instance_and_the_request_carries_fact_content_as_base64()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var request = StartBody(served, "workflow.step", "T-1", next: false, ("fact:spec", "spec.md", "the spec"));

        var started = await Start(served, request);

        Assert.Equal(HttpStatusCode.OK, started.Status);
        Assert.Contains("\"content\": \"dGhlIHNwZWM=\"", ApiGolden.Reindent(request), StringComparison.Ordinal);   // 'the spec' as base64: StartFact has no converter. The server-side decode is proven by started.json's factShas (the sha256 of 'the spec').
        ApiGolden.Match(request, _harness.Home, "workflow-instances-start", "request");
        ApiGolden.Match(started.Body, _harness.Home, "workflow-instances-start", "started", _drawn);
    }

    [Fact(Timeout = 120_000)]
    public async Task Start_refusals_on_a_live_recipe_answer_their_pinned_bodies()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var spec = ("fact:spec", "spec.md", "the spec");

        var malformed = await Start(served, StartBody(served, "workflow.step", "no such item", next: false, spec));
        var noInput = await Start(served, StartBody(served, "workflow.step", "T-4", next: false));
        var mismatch = await Start(served, StartBody(served, "workflow.step", "T-5", next: false, spec, ("fact:item", "item.json", """{ "id": "T-9", "repo": "agentic-workflow", "spec": "the spec" }""")));
        var first = await Start(served, StartBody(served, "workflow.step", "T-3", next: false, spec));
        var live = await Start(served, StartBody(served, "workflow.step", "T-3", next: true, spec));

        // The wired SelfItems stub resolves any well-formed key to itself, so this host has no not-found refusal yet; a malformed reference is SND-CLI-003.
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(("SND-CLI-003", HttpStatusCode.BadRequest), (malformed.Code, malformed.Status));
        Assert.Equal(("SND-CLI-003", HttpStatusCode.BadRequest), (noInput.Code, noInput.Status));
        Assert.Equal(("SND-WF-014", HttpStatusCode.BadRequest), (mismatch.Code, mismatch.Status));
        Assert.Equal(("SND-WF-012", HttpStatusCode.Conflict), (live.Code, live.Status));
        ApiGolden.Match(malformed.Body, _harness.Home, "workflow-instances", "start-item-malformed", _drawn);
        ApiGolden.Match(noInput.Body, _harness.Home, "workflow-instances", "start-missing-input", _drawn);
        ApiGolden.Match(mismatch.Body, _harness.Home, "workflow-instances", "start-item-mismatch", _drawn);
        ApiGolden.Match(live.Body, _harness.Home, "workflow-instances", "start-earlier-attempt-live", _drawn);

        // Documents the missing check, no golden: a well-formed unknown item starts. It should become SND-ITEM-404 when the P3 item store replaces SelfItems.
        var unknown = await Start(served, StartBody(served, "workflow.step", "T-999", next: false, spec));
        Assert.Equal(HttpStatusCode.OK, unknown.Status);
    }

    [Fact(Timeout = 120_000)]
    public async Task Start_refusals_on_a_recipe_that_cannot_start_answer_their_pinned_bodies()
    {
        var step = WorkflowsHarness.Shipped("sindri/workflow.step.yaml");
        var original = _harness.Approve(step);
        _ = _harness.Approve(Changed(step, "version: 2\n", "version: 3\n"));
        _harness.Revoke(original);   // v3 stays approved, so the pinned v2 is found and then refused as revoked
        _ = _harness.RegisterLegacy(Changed(Changed(step, "id: workflow.step\n", "id: workflow.legacy\n"),
            "inputs:\n  - { key: fact:spec, label: \"Spec\", type: text, kind: text }\n", "inputs: [fact:spec]\n"));
        Drifted(Changed(step, "id: workflow.step\n", "id: workflow.drifted\n"));
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var spec = ("fact:spec", "spec.md", "the spec");

        var legacy = await Start(served, StartBody(served, "workflow.legacy", "T-6", next: false, spec));
        var revoked = await Start(served, StartBody(served, "workflow.step@" + original.Hash.Digest, "T-7", next: false, spec));
        var drifted = await Start(served, StartBody(served, "workflow.drifted", "T-8", next: false, spec));

        Assert.Equal(("SND-RECIPE-001", HttpStatusCode.BadRequest), (legacy.Code, legacy.Status));
        Assert.Equal(("SND-RECIPE-005", HttpStatusCode.Conflict), (revoked.Code, revoked.Status));
        Assert.Equal(("SND-RECIPE-006", HttpStatusCode.Conflict), (drifted.Code, drifted.Status));
        ApiGolden.Match(legacy.Body, _harness.Home, "workflow-instances", "start-recipe-legacy", _drawn);
        ApiGolden.Match(revoked.Body, _harness.Home, "workflow-instances", "start-recipe-revoked", _drawn);
        ApiGolden.Match(drifted.Body, _harness.Home, "workflow-instances", "start-recipe-drifted", _drawn);
    }

    /// <summary>Replaces <paramref name="needle"/> in <paramref name="text"/>, failing when it is missing, so a setup edit can't silently do nothing.</summary>
    private static string Changed(string text, string needle, string replacement)
    {
        var changed = text.Replace(needle, replacement, StringComparison.Ordinal);
        Assert.NotEqual(text, changed, StringComparer.Ordinal);
        return changed;
    }

    /// <summary>Registers <paramref name="yaml"/> and approves it with block pins that differ from this build's, as a block that changed since the approval leaves it.</summary>
    private void Drifted(string yaml)
    {
        var registered = _harness.Register(yaml);
        using var sindri = _harness.Sindri();
        var ct = TestContext.Current.CancellationToken;
        var json = sindri.GetRequiredService<JsonSerializerOptions>();
        var recipe = sindri.GetRequiredService<IRecipeReader>().Read(yaml, "recipe.yaml").Document!.Recipe;
        var pins = PinRules.Pins(recipe, sindri.GetRequiredService<BlockCatalog>(), [], json);
        Assert.NotEmpty(pins.Blocks);
        var stale = pins with { Blocks = [.. pins.Blocks.Select(static p => p with { Hash = DescriptorHash.From("sha256:" + new string('0', 64)) })] };
        var approved = sindri.GetRequiredService<IRecipeApprovals>()
            .Approve(new RecipeApproval(registered.Id, registered.Version, registered.Hash, "human:test", RealTime.NowOffset, ApprovalVia.Desk, Base: null, stale), ct).AsTask().GetAwaiter().GetResult();
        Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
    }

    /// <summary>The body the UI posts to start <paramref name="recipe"/> on <paramref name="item"/>: the request record as the host's own options write it.</summary>
    private static string StartBody(Served served, string recipe, string item, bool next, params (string Key, string Name, string Content)[] facts) =>
        JsonSerializer.Serialize(
            new StartInstanceRequest(recipe, item, next, facts.ToDictionary(static f => FactKey.From(f.Key), static f => new StartFact(f.Name, System.Text.Encoding.UTF8.GetBytes(f.Content)))),
            served.Sindri.GetRequiredService<JsonSerializerOptions>());

    private static async Task<Answer> Start(Served served, string body) => await served.Http.Ask(Post(Instances, body, Human(Token(served))));

    private static string GoldenPath(string scenario)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, "tests", "Aw.Sindri.Tests", "Golden", "Api", "workflow-instance", scenario + ".json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException("no golden " + scenario);
    }

    private static string[] Keys(JsonElement e) => [.. e.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal)];

    private static string[] Cells(JsonElement e) => [.. e.GetProperty("states").EnumerateArray().Select(static s =>
        $"{s.GetProperty("order")}/{(s.TryGetProperty("loops", out var l) ? string.Join(',', l.EnumerateArray().Select(static p => $"{p.GetProperty("with")}:{p.GetProperty("max")}")) : "-")}/{(s.TryGetProperty("desk", out var d) ? d.ValueKind.ToString() : "-")}")];

    private static string[] FactStatuses(JsonElement e) => [.. e.GetProperty("facts").EnumerateArray().Select(static f => $"{f.GetProperty("key")}={f.GetProperty("status")}")];

    private static string Token(Served served) => served.Sindri.GetRequiredService<Core.Auth.HeldHumanToken>().Current!.Value;

    private async Task<string> Started(Served served, string item)
    {
        var run = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File(item + "-spec.md", "the spec"));
        Assert.True(run.Exit == 0, run.Stderr);
        _ = await served.Until(item + "/step/1", static v => v.Task is not null);
        return await IdOfItem(served, item);
    }

    private static async Task<string> IdOfItem(Served served, string item)
    {
        var body = await Read(served, $"/sindri/workflow/instances?address={Uri.EscapeDataString(item)}");
        return JsonDocument.Parse(body).RootElement.GetProperty("instances")[0].GetProperty("id").GetString()!;
    }

    private static async Task<string> Read(Served served, string path)
    {
        var answer = await served.Http.Ask(Get(path, Human(Token(served))));
        Assert.True(answer.Status == HttpStatusCode.OK, answer.Body);
        return answer.Body;
    }

    private static async Task<InstanceList> ListUntil(Served served, string path, Func<InstanceList, bool> holds)
    {
        var options = served.Sindri.GetRequiredService<JsonSerializerOptions>();
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        while (true)
        {
            var list = JsonSerializer.Deserialize<InstanceList>(await Read(served, path), options)!;   // visibility is eventually consistent
            if (holds(list) || RealTime.NowDateTime > deadline)
            {
                return list;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
    }
}
