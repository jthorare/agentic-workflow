using System.Text.Json;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Testing.Views;
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
        ("workflow-recipe", "workflow.no-outputs"), ("workflow-recipe", "workflow.f5-fields"), ("workflow-instance", "f5-state-times"), ("workflow-instance", "f5-rerun-failed"), ("workflow-instance", "f5-desk-recommended"), ("workflow-instance", "lens-ask-multi"), ("workflow-recipe", "workflow.design"),
        .. FixtureRecipes.All.Select(r => ("workflow-recipe", r.Id)),
        ("workflow-instance-fact", "scope-map"), ("workflow-instance-fact", "packet"), ("workflow-instance-fact", "evidence"), ("workflow-instance-fact", "payload"), ("workflow-instance-fact", "packet-unfrozen"),
        ("workflow-instance-fact", "text"), ("workflow-instance-fact", "truncated"), ("workflow-instance-fact", "pr-status"), ("workflow-instance-fact", "lens-ask-escalation"),
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

    private static string Body(string slug, string scenario, JsonSerializerOptions options) => (slug, scenario) switch
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
        ("workflow-status", "parked-child") => Json(WorkflowViewFixtures.StatusParkedChild(), options),
        ("workflow-status", "unreachable-child") => Json(WorkflowViewFixtures.StatusUnreachableChild(), options),
        ("workflow-status", "escalated-child") => Json(WorkflowViewFixtures.StatusEscalatedChild(), options),
        ("workflow-status", "mirrored-budget-no-child") => Json(WorkflowViewFixtures.StatusMirroredBudgetNoChild(), options),
        ("workflow-status", "mirrored-budget") => Json(WorkflowViewFixtures.StatusMirroredBudget(), options),
        ("workflow-status", "desk-task") => Json(WorkflowViewFixtures.StatusDeskTask(), options),
        ("workflow-status", "parked-budget") => Json(WorkflowViewFixtures.StatusParkedBudget(), options),
        ("workflow-status", "escalated-gate") => Json(WorkflowViewFixtures.StatusEscalatedGate(), options),
        ("workflow-status", "taken-over") => Json(WorkflowViewFixtures.StatusTakenOver(), options),
        ("workflow-status", "done") => Json(WorkflowViewFixtures.StatusDone(), options),
        ("workflow-status", "agent-run") => Json(WorkflowViewFixtures.StatusAgentRun(), options),
        ("workflow-status", "parked-drift") => Json(WorkflowViewFixtures.StatusParkedDrift(), options),
        ("workflow-status", "needs-answer-step") => Json(WorkflowViewFixtures.StatusNeedsAnswerStep(), options),
        ("workflow-status", "needs-answer-wait") => Json(WorkflowViewFixtures.StatusNeedsAnswerWait(), options),
        ("workflow-instance", "f5-rerun-failed") => Json(WorkflowViewFixtures.F5RerunFailed(), options),
        ("workflow-instance", "f5-desk-recommended") => Json(WorkflowViewFixtures.F5DeskRecommended(), options),
        ("workflow-instance", "lens-ask-multi") => Json(WorkflowViewFixtures.LensAskMulti(), options),
        ("workflow-recipe", "workflow.no-outputs") => Json(WorkflowViewFixtures.NoOutputsRecipe(), options),
        ("workflow-recipe", "workflow.f5-fields") => Json(WorkflowViewFixtures.F5FieldsRecipe(), options),
        ("workflow-instance", "f5-state-times") => Json(WorkflowViewFixtures.F5StateTimes(), options),
        ("workflow-recipe", var id) when FixtureRecipes.All.Append(FixtureRecipes.Design).Any(r => r.Id == id) => Json(WorkflowViewFixtures.Recipe(id), options),
        ("workflow-instance-fact", _) => Json(WorkflowViewFixtures.Fact(scenario), options),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such workflow view scenario"),
    };

    private static string Json<T>(T value, JsonSerializerOptions options) =>
        ApiGolden.Reindent(JsonSerializer.Serialize(value, options));
}
