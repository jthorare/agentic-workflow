using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'GET /setup' (the setup list): the route's body for each way the six steps can stand. The sources are the set facts of <see cref="InMemorySetupFacts"/>, because the real
/// ones read this machine (Docker, a Claude Code config); the real adapters are tested apart (SetupFactsTests, the served-route test of this group).
/// </summary>
internal static class SetupScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("setup", "first-run"), ("setup", "profile-needs-approval"), ("setup", "running-progress"), ("setup", "failed"), ("setup", "all-done"), ("setup", "onyx-no-docker"), ("setup", "map-retrying"), ("setup", "onyx-skipped"), ("setup", "profile-preview"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts a host whose setup sources answer the scenario's facts.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct)
    {
        var facts = Facts(scenario);
        return GoldenHost.Start(temporary: true, ct, extra: services => services.Replace(ServiceDescriptor.Singleton<ISetupFacts>(facts)));
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        _ = ct;
        _ = slug;
        _ = scenario;
        return (await host.Http.Ask(Get("/sindri/setup", Human(host.Token.Value)))).Body;
    }

    private static InMemorySetupFacts Facts(string scenario) => scenario switch
    {
        "profile-needs-approval" => new InMemorySetupFacts
        {
            RepoAnswer = InMemorySetupFacts.OneRepo,
            ProfileAnswer = new ProfileFact(ProfilePresence.Unapproved),
            RecipesAnswer = new RecipesFact(26, 0),
        },
        "profile-preview" => new InMemorySetupFacts
        {
            RepoAnswer = InMemorySetupFacts.OneRepo,
            ProfileAnswer = new ProfileFact(ProfilePresence.Unapproved) { Preview = _previewLines },
            RecipesAnswer = new RecipesFact(26, 0),
        },
        "running-progress" => Done(map: new MapFact(MapPhase.Building, new SetupProgress(40, 100, "files")) { Items = Lines(SetupState.Running) }),
        "failed" => Done(map: new MapFact(MapPhase.Failed, FailureCode: "SND-CONTEXT-042") { Items = Lines(SetupState.Failed) }),
        "map-retrying" => Done(map: new MapFact(MapPhase.Building, new SetupProgress(0, 1, "files")) { Items = Lines(SetupState.Running), Retry = new MapRetry(TimeSpan.FromMinutes(5), "the code index was busy") }),
        "onyx-skipped" => Done(onyx: new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null) { Deferred = true }),
        "onyx-no-docker" => Done(onyx: new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: "SND-ONYX-001")),
        "all-done" => InMemorySetupFacts.AllDone(),
        _ => new InMemorySetupFacts { RecipesAnswer = new RecipesFact(26, 0) },
    };

    private static readonly SetupPreviewLine[] _previewLines =
    [
        new("repo", "Works in demo (/work/demo)."),
        new("service", "Runs the observe service every 1h."),
        new("allows", "Workflows may: pr.open, pr.comment."),
        new("limit", "Warns you when an agent has used 80% of its daily budget."),
    ];

    private static SetupItems Lines(SetupState state) => new([new SetupItem("demo", state, Detail: null)]);

    private static InMemorySetupFacts Done(MapFact? map = null, OnyxFact? onyx = null)
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.MapAnswer = map ?? facts.MapAnswer;
        facts.OnyxAnswer = onyx ?? facts.OnyxAnswer;
        return facts;
    }
}
