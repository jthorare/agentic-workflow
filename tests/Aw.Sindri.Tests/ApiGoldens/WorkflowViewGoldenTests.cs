using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The golden contract of the rev-8 views (API spec section 3.8; UI screens spec E-20, E-22, E-23): each scenario's fixture
/// (<see cref="WorkflowViewFixtures"/>) serialized through the host's own JSON options, pinned at
/// 'Golden/Api/&lt;slug&gt;/&lt;scenario&gt;.json'. The UI lanes build their fake ISindriApi on these files. The files change only
/// at the points docs/csharp/sindri.md lists (engine P4b wording, real recipe hashes, the served not-found body, the P8 and schema v2 re-records); each is a contract change the UI lane is told.
/// </summary>
public sealed class WorkflowViewGoldenTests : IAsyncLifetime
{
    private GoldenHost? _host;

    /// <summary>Every (slug, scenario) pair this class pins, in the order of the plan's golden list.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("workflow-instances", "root"), ("workflow-instances", "recipe-filter"), ("workflow-instances", "empty"), ("workflow-instances", "open"), ("workflow-instances", "preview-missing-input"),
        ("workflow-instance", "project-build"), ("workflow-instance", "incident-loop"), ("workflow-instance", "triage-desk"),
        ("workflow-instance", "escalated-gate"), ("workflow-instance", "dev-desk-round-trip"), ("workflow-instance", "dev-triage-three-state"),
        ("workflow-instance", "item-produce"), ("workflow-instance", "project-many-children"), ("workflow-instance", "transition-list"), ("workflow-instance", "done"),
        ("workflow-status", "parked-child"), ("workflow-status", "unreachable-child"), ("workflow-status", "escalated-child"), ("workflow-status", "mirrored-budget"), ("workflow-status", "mirrored-budget-no-child"), ("workflow-status", "desk-task"), ("workflow-status", "parked-budget"),
        ("workflow-status", "escalated-gate"), ("workflow-status", "taken-over"), ("workflow-status", "done"), ("workflow-status", "agent-run"),
        ("workflow-status", "parked-drift"), ("workflow-status", "needs-answer-step"), ("workflow-status", "needs-answer-wait"),
        ("workflow-recipe", "workflow.no-outputs"), ("workflow-recipe", "workflow.f5-fields"), ("workflow-recipe", "workflow.scoping"), ("workflow-instance", "f5-state-times"), ("workflow-instance", "skill-step"), ("workflow-instance", "f5-rerun-failed"), ("workflow-instance", "f5-desk-recommended"), ("workflow-instance", "lens-ask-multi"), ("workflow-instance", "scoping-interview"), ("workflow-instance", "desk-reviews"), ("workflow-instance", "agent-run-session"), ("workflow-status", "parked-dispatch-refused"), ("workflow-status", "agent-run-session"), ("workflow-instance", "babysitter-ready-verbs"), ("workflow-recipe", "workflow.pr-babysitter"), ("workflow-recipe", "workflow.design"),
        .. FixtureRecipes.All.Select(r => ("workflow-recipe", r.Id)),
        ("workflow-instance-fact", "scope-map"), ("workflow-instance-fact", "packet"), ("workflow-instance-fact", "evidence"), ("workflow-instance-fact", "payload"), ("workflow-instance-fact", "packet-unfrozen"),
        ("workflow-instance-fact", "text"), ("workflow-instance-fact", "truncated"), ("workflow-instance-fact", "pr-status"), ("workflow-instance-fact", "lens-ask-escalation"), ("workflow-instance-fact", "scoping-questions"),
    ];

    public static TheoryData<string, string> Rows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (slug, scenario) in All)
        {
            rows.Add(slug, scenario);
        }

        return rows;
    }

    public async ValueTask InitializeAsync() => _host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public void View_matches_its_golden_file(string slug, string scenario)
    {
        var host = _host ?? throw new InvalidOperationException("the host isn't started");
        var options = host.App.Services.GetRequiredService<JsonSerializerOptions>();

        ApiGolden.Match(Body(slug, scenario, options), host.Home, slug, scenario);
    }

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public void View_reads_back_into_its_record_and_writes_the_same_body(string slug, string scenario)
    {
        var host = _host ?? throw new InvalidOperationException("the host isn't started");
        var options = host.App.Services.GetRequiredService<JsonSerializerOptions>();
        var body = Body(slug, scenario, options);
        var again = slug switch
        {
            "workflow-instances" => Json(JsonSerializer.Deserialize<InstanceList>(body, options), options),
            "workflow-instance" => Json(JsonSerializer.Deserialize<InstanceDetail>(body, options), options),
            "workflow-status" => Json(JsonSerializer.Deserialize<StatusFields>(body, options), options),
            "workflow-recipe" => Json(JsonSerializer.Deserialize<RecipeView>(body, options), options),
            "workflow-instance-fact" => Json(JsonSerializer.Deserialize<FactView>(body, options), options),
            _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "no record for this slug"),
        };

        Assert.Equal(body, again);
    }

    // The scoping recipe golden is the shipped file's own view with its real hash (the scoping-interview instance golden carries that hash).
    private static string Body(string slug, string scenario, JsonSerializerOptions options) =>
        (slug, scenario) == ("workflow-recipe", "workflow.scoping") ? Json(ShippedRecipeView("sindri/workflow.scoping.yaml"), options) : Shipped(slug, scenario, options) ?? FixtureBody(slug, scenario, options);

    private static RecipeView ShippedRecipeView(string path)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), path);
        return RecipeViews.Of(document.Recipe, new RecipeVersion(document.Recipe.Id, document.Recipe.Version, document.Hash));
    }

    private static string FixtureBody(string slug, string scenario, JsonSerializerOptions options) => (slug, scenario) switch
    {
        ("workflow-instances", "root") => Json(WorkflowViewFixtures.RootList(), options),
        ("workflow-instances", "recipe-filter") => Json(WorkflowViewFixtures.RecipeFilter(), options),
        ("workflow-instances", "open") => Json(WorkflowViewFixtures.OpenList(), options),
        ("workflow-instances", "preview-missing-input") => Json(WorkflowViewFixtures.PreviewMissingInput(), options),
        ("workflow-instances", "empty") => Json(WorkflowViewFixtures.EmptyList(), options),
        ("workflow-instance", "project-build") => Json(WorkflowViewFixtures.ProjectBuild(), options),
        ("workflow-instance", "incident-loop") => Json(WorkflowViewFixtures.IncidentLoop(), options),
        ("workflow-instance", "triage-desk") => Json(WorkflowViewFixtures.TriageDesk(), options),
        ("workflow-instance", "escalated-gate") => Json(WorkflowViewFixtures.EscalatedGate(), options),
        ("workflow-instance", "dev-desk-round-trip") => Json(WorkflowViewFixtures.DeskRoundTrip(), options),
        ("workflow-instance", "dev-triage-three-state") => Json(WorkflowViewFixtures.TriageThreeState(), options),
        ("workflow-instance", "item-produce") => Json(WorkflowViewFixtures.ItemProduce(), options),
        ("workflow-instance", "project-many-children") => Json(WorkflowViewFixtures.ProjectManyChildren(), options),
        ("workflow-instance", "transition-list") => Json(WorkflowViewFixtures.TransitionList(), options),
        ("workflow-instance", "done") => Json(WorkflowViewFixtures.Done(), options),
        ("workflow-instance", "f5-rerun-failed") => Json(WorkflowViewFixtures.F5RerunFailed(), options),
        ("workflow-instance", "f5-desk-recommended") => Json(WorkflowViewFixtures.F5DeskRecommended(), options),
        ("workflow-instance", "lens-ask-multi" or "scoping-interview") => Json(scenario == "lens-ask-multi" ? WorkflowViewFixtures.LensAskMulti() : WorkflowViewFixtures.ScopingInterview(), options),
        ("workflow-recipe", "workflow.no-outputs") => Json(WorkflowViewFixtures.NoOutputsRecipe(), options),
        ("workflow-recipe", "workflow.f5-fields") => Json(WorkflowViewFixtures.F5FieldsRecipe(), options),
        ("workflow-instance", "f5-state-times") => Json(WorkflowViewFixtures.F5StateTimes(), options),
        ("workflow-instance", "skill-step") => Json(WorkflowViewFixtures.SkillStep(), options),
        ("workflow-recipe", var id) when FixtureRecipes.All.Append(FixtureRecipes.Design).Any(r => r.Id == id) => Json(WorkflowViewFixtures.Recipe(id), options),
        ("workflow-instance-fact", _) => Json(WorkflowViewFixtures.Fact(scenario), options),
        _ => Opened(slug, scenario, options),
    };

    /// <summary>The q65 goldens: what a desk reviews, the run's session and a park's reason code.</summary>
    private static string Opened(string slug, string scenario, JsonSerializerOptions options) => (slug, scenario) switch
    {
        ("workflow-instance", "desk-reviews") => Json(WorkflowViewFixtures.DeskReviews(), options),
        ("workflow-instance", "agent-run-session") => Json(WorkflowViewFixtures.AgentRunSession(), options),
        ("workflow-status", "parked-dispatch-refused") => Json(WorkflowViewFixtures.StatusParkedDispatchRefused(), options),
        ("workflow-status", "agent-run-session") => Json(WorkflowViewFixtures.StatusAgentRunSession(), options),
        ("workflow-status", _) => Json(Status(scenario), options),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such workflow view scenario"),
    };

    private static StatusFields Status(string scenario) => scenario switch
    {
        "parked-child" => WorkflowViewFixtures.StatusParkedChild(),
        "unreachable-child" => WorkflowViewFixtures.StatusUnreachableChild(),
        "escalated-child" => WorkflowViewFixtures.StatusEscalatedChild(),
        "mirrored-budget-no-child" => WorkflowViewFixtures.StatusMirroredBudgetNoChild(),
        "mirrored-budget" => WorkflowViewFixtures.StatusMirroredBudget(),
        "desk-task" => WorkflowViewFixtures.StatusDeskTask(),
        "parked-budget" => WorkflowViewFixtures.StatusParkedBudget(),
        "escalated-gate" => WorkflowViewFixtures.StatusEscalatedGate(),
        "taken-over" => WorkflowViewFixtures.StatusTakenOver(),
        "done" => WorkflowViewFixtures.StatusDone(),
        "agent-run" => WorkflowViewFixtures.StatusAgentRun(),
        "parked-drift" => WorkflowViewFixtures.StatusParkedDrift(),
        "needs-answer-step" => WorkflowViewFixtures.StatusNeedsAnswerStep(),
        "needs-answer-wait" => WorkflowViewFixtures.StatusNeedsAnswerWait(),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such workflow status scenario"),
    };

    /// <summary>
    /// The goldens built from the shipped pr-babysitter through the engine's own views, not a hand-made fixture (UI spec E-13, PB spec API18): the instance at 'ready', whose recipe verbs are the detail's
    /// 'allowedCommands', and the recipe it runs, which the app's contract needs beside any instance golden. The recipe's hash is a fixed stamp so an edit to the recipe does not move them; its layout still follows the recipe.
    /// </summary>
    private static string? Shipped(string slug, string scenario, JsonSerializerOptions options)
    {
        var recipe = EvalComposition.Recipe("sindri/workflow.pr-babysitter.yaml") with { Hash = RecipeHash.From("sha256:" + new string('b', 64)) };
        var version = new RecipeVersion(recipe.Id, recipe.Version, recipe.Hash.Value);
        return (slug, scenario) switch
        {
            ("workflow-recipe", "workflow.pr-babysitter") => Json(RecipeViews.Of(recipe, version, displayName: null, EvalComposition.Catalog), options),
            ("workflow-instance", "babysitter-ready-verbs") => Json(BabysitterReady(recipe, version), options),
            _ => null,
        };
    }

    private static InstanceDetail BabysitterReady(Recipe recipe, RecipeVersion version)
    {
        var interpreted = new InterpretedRecipe(version, recipe, EvalComposition.Catalog, new Dictionary<FactKey, InputFact>());   // why: the shipped recipe is read with it
        var view = InstanceViews.Of(EngineFixture.At(recipe, "ready"), interpreted, DateTimeOffset.UnixEpoch, timeoutAt: null, new InstanceViewContext(Version: 1, Children: null));
        return InstanceViews.Detail(view, new InstanceDetailContext("Add the offline sync route", Parent: null, new ViewCounts(0, 0, 0, Context: null), DateTimeOffset.UnixEpoch));
    }

    private static string Json<T>(T value, JsonSerializerOptions options) =>
        ApiGolden.Reindent(JsonSerializer.Serialize(value, options));
}
