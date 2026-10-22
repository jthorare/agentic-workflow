using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// The rev-8 workflow goldens (API spec §3.8, served by #213) through the app's mirror records. The files are read from
/// disk and enumerated, never listed, so a golden added or renamed by the API lane is read the moment it lands.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowGoldenTests
{
    private static readonly string[] _workflowSlugs = ["workflow-instances", "workflow-instance", "workflow-instance-fact", "workflow-status", "workflow-recipe"];

    // What each slug is written back as after the app read it.
    private static readonly Dictionary<string, Func<byte[], Result<byte[]>>> _roundTrip = new(StringComparer.Ordinal)
    {
        ["workflow-instances"] = RoundTrip<InstanceList>,
        ["workflow-instance"] = RoundTrip<InstanceDetail>,
        ["workflow-instance-fact"] = RoundTrip<FactView>,
        ["workflow-status"] = RoundTrip<StatusFields>,
        ["workflow-recipe"] = RoundTrip<RecipeView>,
    };

    private static IEnumerable<(string Slug, string Scenario, byte[] Body)> Served() =>
        from golden in GoldenFiles.All()
        where golden.Kind == "Api" && _workflowSlugs.Contains(golden.Slug, StringComparer.Ordinal) && !IsRefusal(golden.Body)
        select (golden.Slug, golden.Scenario, Encoding.UTF8.GetBytes(golden.Body));

    private static bool IsRefusal(string body) => JsonNode.Parse(body) is JsonObject json && json["traceId"] is not null;

    [Fact]
    public void Every_served_workflow_golden_is_found_on_disk_for_every_slug()
    {
        var served = Served().ToList();

        foreach (var slug in _workflowSlugs)
        {
            Assert.Contains(served, g => g.Slug == slug);
        }
    }

    [Fact]
    public void Nullable_members_are_present_and_null_never_missing()
    {
        // Every member the app mirrors is written back: a golden that left a nullable member out, or carries one the app
        // doesn't know, writes back differently. (RespectRequiredConstructorParameters makes a missing one a read error.)
        foreach (var (slug, scenario, body) in Served())
        {
            var written = _roundTrip[slug](body);

            Assert.True(written.IsSuccess, $"{slug}/{scenario}: {(written.IsError ? written.Error.Message : string.Empty)}");
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(body), JsonNode.Parse(written.Value)), $"{slug}/{scenario}: the app writes back what it read");
        }
    }

    [Fact]
    public void Workflow_status_goldens_read_as_StatusFields_and_equal_the_views_current_status()
    {
        var views = Served().Where(g => g.Slug == "workflow-instance")
            .Select(g => (g.Scenario, Status: JsonNode.Parse(g.Body)!["current"]!["status"]!)) // why: every instance golden has current.status
            .ToList();
        var matched = 0;

        foreach (var (_, scenario, body) in Served().Where(g => g.Slug == "workflow-status"))
        {
            var status = AwJson.Deserialize<StatusFields>(body, SindriApiClient.Json);
            Assert.True(status.IsSuccess, $"workflow-status/{scenario}");
            Assert.False(string.IsNullOrEmpty(status.Value.Text), $"workflow-status/{scenario}: no text");

            var sameAddress = views.Where(v => string.Equals((string?)v.Status["header"]!["address"], status.Value.Header.Address, StringComparison.Ordinal)).ToList();
            if (sameAddress.Count > 0)
            {
                var node = JsonNode.Parse(body);
                Assert.True(sameAddress.Exists(v => JsonNode.DeepEquals(v.Status, node)), $"workflow-status/{scenario} differs from the current.status of {string.Join(", ", sameAddress.Select(v => v.Scenario))}");
                matched++;
            }
        }

        Assert.True(matched > 0, "no status golden has an instance golden to equal");
    }

    // A budget with no cap is served with "limit": null (the figure is the used count alone).
    [Fact]
    public void A_budget_served_with_a_null_limit_reads_as_no_cap()
    {
        var body = JsonNode.Parse(GoldenFiles.Api("workflow-status", "parked-budget"))!;
        var used = body["budget"]!["used"]!.GetValue<long>();
        body["budget"]!["limit"] = null;
        body["budget"]!["instance"]!["limit"] = null;

        var status = AwJson.Deserialize<StatusFields>(Encoding.UTF8.GetBytes(body.ToJsonString()), SindriApiClient.Json);

        Assert.True(status.IsSuccess, status.IsError ? status.Error.Message : string.Empty);
        var budget = status.Value.Budget!; // why: the golden has a budget
        Assert.Equal(used, budget.Used);
        Assert.Null(budget.Limit);
        Assert.Null(budget.Instance.Limit);
    }

    [Fact]
    public void Not_found_reads_as_ProblemDetails_with_the_routes_code()
    {
        var refusals = GoldenFiles.All().Where(g => g.Kind == "Api" && g.Scenario == "not-found" && _workflowSlugs.Contains(g.Slug, StringComparer.Ordinal)).ToList();
        Assert.True(refusals.Count >= 3, "the instance, fact and recipe routes each have a not-found golden");

        foreach (var (_, slug, _, body) in refusals)
        {
            var problem = AwJson.Deserialize<ProblemView>(Encoding.UTF8.GetBytes(body), SindriApiClient.Json).Value;
            var error = SindriApiClient.Refusal(HttpStatusCode.NotFound, Encoding.UTF8.GetBytes(body));

            var code = string.Equals(slug, "workflow-recipe", StringComparison.Ordinal) ? "SND-RECIPE-002" : "SND-ITEM-404";
            Assert.Equal(code, problem.Title);
            Assert.NotNull(error);
            Assert.Equal((code, ErrorKind.NotFound, problem.Detail), (error.Code, error.Kind, error.Message));
            Assert.False(string.IsNullOrEmpty(problem.Detail), slug);
        }
    }

    // Each recipe golden is named by the id its body carries (read from disk, never listed here), and agrees with the
    // instances that run the recipe (the same states).
    [Fact]
    public void Each_recipe_golden_is_named_by_its_id_and_agrees_with_the_instances_that_run_it()
    {
        var goldens = GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-recipe" && !g.Body.Contains("traceId", StringComparison.Ordinal)).ToList();
        var instances = GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-instance" && !g.Body.Contains("traceId", StringComparison.Ordinal)).Select(g => JsonNode.Parse(g.Body)!).ToList();

        Assert.NotEmpty(goldens);
        Assert.Equal(goldens.Select(g => g.Scenario).Order(StringComparer.Ordinal), WorkflowAnswers.NamedRecipeGoldens.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (_, _, scenario, body) in goldens)
        {
            var recipe = JsonNode.Parse(body)!;
            var id = (string)recipe["id"]!; // why: a recipe golden starts with its id
            Assert.Equal(scenario, id);
            Assert.Matches("^sha256:[0-9a-f]{64}$", (string)recipe["hash"]!); // why: and its hash
            Assert.Equal(id, WorkflowAnswers.NamedRecipeGoldens[scenario]);
            var states = recipe["states"]!.AsArray().Select(c => (string)c!["id"]!).ToList(); // why: a recipe lists states
            var running = instances.Where(i => (string?)i["recipe"]!["id"] == id).ToList(); // why: an instance names its recipe
            Assert.All(running, i => Assert.Equal(states, i["states"]!.AsArray().Select(c => (string)c!["id"]!), StringComparer.Ordinal)); // why: an instance lists its states
        }
    }

    // The fake serves a fact golden only for the instance goldens its table binds it to: the table names every served fact
    // golden (an unfrozen packet fits no instance yet, so its list is empty), and each bound instance lists the golden's key.
    [Fact]
    public void Every_served_fact_golden_is_bound_and_each_bound_instance_lists_its_key()
    {
        var served = GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-instance-fact" && !g.Body.Contains("traceId", StringComparison.Ordinal)).ToList();
        var instances = GoldenFiles.All().Where(g => g.Kind == "Api" && g.Slug == "workflow-instance" && !g.Body.Contains("traceId", StringComparison.Ordinal)).ToDictionary(g => g.Scenario, g => JsonNode.Parse(g.Body)!, StringComparer.Ordinal);

        Assert.Equal(served.Select(g => g.Scenario).Order(StringComparer.Ordinal), WorkflowAnswers.FactGoldens.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(WorkflowAnswers.FactGoldens["packet-unfrozen"]);
        foreach (var (_, _, scenario, body) in served)
        {
            var key = (string)JsonNode.Parse(body)!["key"]!; // why: a fact golden has its key
            foreach (var instance in WorkflowAnswers.FactGoldens[scenario])
            {
                Assert.True(instances.ContainsKey(instance), $"{scenario}: no instance golden {instance}");
                Assert.Contains(instances[instance]["facts"]!.AsArray(), f => (string?)f!["key"] == key); // why: an instance lists its facts
            }
        }
    }

    [Fact]
    public void A_recipe_not_found_is_the_apis_refusal_golden()
    {
        var refused = WorkflowAnswers.Recipe("workflow.none", hash: string.Empty);

        Assert.Equal(HttpStatusCode.NotFound, refused.Status);
        Assert.Equal(GoldenFiles.Api("workflow-recipe", "not-found"), refused.Body);   // SND-RECIPE-002, served as the API wrote it
    }

    [Fact]
    public void A_recipe_asked_for_by_a_hash_it_does_not_have_is_the_apis_refusal_golden()
    {
        var id = WorkflowAnswers.NamedRecipeGoldens.Values.First();
        var own = (string)JsonNode.Parse(GoldenFiles.Api("workflow-recipe", id))!["hash"]!; // why: a recipe golden carries its hash

        Assert.Equal(HttpStatusCode.OK, WorkflowAnswers.Recipe(id, own).Status);
        var refused = WorkflowAnswers.Recipe(id, "sha256:" + new string('0', 64));

        Assert.Equal(HttpStatusCode.NotFound, refused.Status);
        Assert.Equal(GoldenFiles.Api("workflow-recipe", "not-found"), refused.Body);
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(12, true)]
    [InlineData(7, false)]
    public void A_recipe_asked_for_by_a_prefix_of_its_hash_is_served_when_the_prefix_has_eight_digits(int digits, bool served)
    {
        var id = WorkflowAnswers.NamedRecipeGoldens.Values.First();
        var own = (string)JsonNode.Parse(GoldenFiles.Api("workflow-recipe", id))!["hash"]!; // why: a recipe golden carries its hash
        var hex = own["sha256:".Length..];

        Assert.Equal(served, WorkflowAnswers.Recipe(id, hex[..digits]).Status == HttpStatusCode.OK);
        Assert.Equal(served, WorkflowAnswers.Recipe(id, "sha256:" + hex[..digits]).Status == HttpStatusCode.OK);
    }

    [Fact]
    public void Unknown_fact_type_reads_as_payload()
    {
        var recipe = JsonNode.Parse(GoldenFiles.Api("workflow-recipe", (string)JsonNode.Parse(GoldenFiles.Api("workflow-instance", "project-build"))!["recipe"]!["id"]!))!; // why: the golden is a JSON object
        recipe["inputs"]![0]!["type"] = "hologram";
        recipe["outputs"]![0]!["type"] = "hologram";
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "project-build"))!; // why: as above
        instance["facts"]![0]!["type"] = "hologram";
        var fact = JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", "text"))!; // why: as above
        fact["type"] = "hologram";

        Assert.Equal(FactType.Payload, Read<RecipeView>(recipe).Inputs[0].Type);
        Assert.Equal(FactType.Payload, Read<RecipeView>(recipe).Outputs[0].Type);
        Assert.Equal(FactType.Payload, Read<InstanceDetail>(instance).Facts[0].Type);
        Assert.Equal(FactType.Payload, Read<FactView>(fact).Type);
    }

    [Fact]
    public void A_fact_type_is_read_by_its_wire_word()
    {
        var scope = Read<FactView>(JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", "scope-map"))!); // why: the golden is a JSON object

        Assert.Equal(FactType.ScopeMap, scope.Type);
    }

    // API rev 8 added words over time: a tone or glyph this build doesn't know still reads, as the quietest look.
    [Fact]
    public void Unknown_chip_tone_reads_as_neutral_and_unknown_glyph_as_pending()
    {
        var list = JsonNode.Parse(GoldenFiles.Api("workflow-instances", "root"))!; // why: the golden is a JSON object
        list["instances"]![0]!["chip"]!["tone"] = "mauve";
        list["instances"]![0]!["states"]![0]!["glyph"] = "halfway";
        list["instances"]![1]!["chip"]!["tone"] = 7;

        var read = Read<InstanceList>(list);

        Assert.Equal(ChipTone.Neutral, read.Instances[0].Chip.Tone);
        Assert.Equal(ChipTone.Neutral, read.Instances[1].Chip.Tone);
        Assert.Equal(StateGlyph.Pending, read.Instances[0].States[0].Glyph);
    }

    private static T Read<T>(JsonNode node)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(node.ToJsonString()), SindriApiClient.Json).Value;

    private static Result<byte[]> RoundTrip<T>(byte[] body)
        where T : class
    {
        var value = AwJson.Deserialize<T>(body, SindriApiClient.Json);
        return value.IsError ? value.Error : AwJson.Serialize(value.Value, SindriApiClient.Json);
    }

    [Fact]
    public void The_failed_run_events_read_with_the_reason_of_the_failed_step()
    {
        var events = Read<InstanceEventsView>(JsonNode.Parse(GoldenFiles.SpecShaped("workflow-instance-events", "failed-run"))!); // why: the golden is a JSON object

        Assert.Equal(47, events.Cursor);
        var failed = Assert.Single(events.Events, e => e.Transition?.Trigger == "step.failed");
        Assert.Equal(("exit", "agent"), (failed.Transition!.Reason, failed.Transition.Via));
        Assert.Equal(61000, events.Events.Select(e => e.Transition?.Tokens).Single(t => t is not null));
    }

    [Fact]
    public void A_block_run_frame_reads_its_actor_and_attempt_and_writes_back_without_the_fields_it_lacks()
    {
        var opened = FrameOf("workflow.transition-block-run-attempt-3-of-3");
        var quiet = FrameOf("workflow.transition-nothing-open");

        Assert.Equal(("produce", "block", "block", 3, 3), (opened.State, opened.Runtime, opened.Actor, opened.Attempt, opened.MaxAttempts));
        Assert.Equal((null, null, null, null), (quiet.Runtime, quiet.Actor, quiet.Attempt, quiet.MaxAttempts));
        var written = JsonNode.Parse(AwJson.Serialize(quiet, SindriApiClient.Json))!.AsObject(); // why: a record writes a JSON object
        Assert.Equal(["state", "entry", "run", "child"], written.Select(p => p.Key), StringComparer.Ordinal);
    }

    // The frame sits at detail.delta.frame of the engine's ledger row.
    private static FrameSummary FrameOf(string row) =>
        AwJson.Deserialize<FrameSummary>(Encoding.UTF8.GetBytes(GoldenFiles.Ledger(row)["detail"]!["delta"]!["frame"]!.ToJsonString()), SindriApiClient.Json).Value;
}
