using Aw.Sindri.Core.Linear;
using Aw.Sindri.Tests.LinearReads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Linear hub's side reads (#2077 Ph3 1a; Linear hub spec 2026-11-04 §3, §4.2, §4.3; contract AGREED by api and ui): 'GET /linear/lookups/{team}' and 'GET /linear/since'. The port behind them
/// (<see cref="ILinearSideFacts"/>) is a <see cref="FakeLinearSideFacts"/> left empty: these rows are the refusals and the empty strip. The clock stands at the epoch.
/// </summary>
internal static class LinearReadScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-lookups", "eng"), ("linear-lookups", "not-synced"), ("linear-lookups", "session-refused"),
        ("linear-since", "three"), ("linear-since", "empty"), ("linear-since", "session-refused"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile over the scenario's facts.</summary>
    public static Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services => services.Replace(ServiceDescriptor.Singleton<ILinearSideFacts>(Facts(slug, scenario))));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario)
    {
        ArgumentNullException.ThrowIfNull(host);
        var caller = scenario == "session-refused" ? ItemScenarios.Session("demo") : ItemScenarios.Human(host);
        var path = (slug, scenario) switch
        {
            ("linear-lookups", "not-synced") => "/sindri/linear/lookups/OPS",
            ("linear-lookups", _) => "/sindri/linear/lookups/ENG",
            ("linear-since", "empty") => "/sindri/linear/since?after=lin_000000000000003f",
            _ => "/sindri/linear/since",
        };
        return (await host.Http.Ask(Get(path, caller))).Body;
    }

    /// <summary>
    /// The facts a scenario's host answers with. 'linear-since/three' is Sam Lee's assignment of ENG-31, a mention
    /// on ENG-12 and ENG-12's move from Todo to In Progress; Context keeps no actor for an assignment or a state move, so only the mention has one, as the real adapter answers.
    /// </summary>
    internal static FakeLinearSideFacts Facts(string slug, string scenario)
    {
        if ((slug, scenario) is ("linear-lookups", "eng"))
        {
            return new FakeLinearSideFacts().WithTeam(Eng());
        }

        if ((slug, scenario) is ("linear-lookups", "not-synced"))
        {
            return new FakeLinearSideFacts().WithTeam(new LinearLookupsResponse(new LinearTeamRef(Uuid: null, "OPS", Name: null), SyncedAt: null, [], [], [], []));
        }

        var sam = new LinearPerson("00000000-0000-4000-8000-0000000000a2", "Sam Lee");
        return (slug, scenario) is ("linear-since", "three")
            ? new FakeLinearSideFacts().WithItems(
                new LinearSinceItem("assigned", "ENG-31", "Export limits: cap the CSV at 50k rows", Actor: null, At(22, 10), From: null, To: null),
                new LinearSinceItem("mentioned", "ENG-12", "Retry budget for the sync worker", sam, At(23, 30), From: null, To: null),
                new LinearSinceItem("state", "ENG-12", "Retry budget for the sync worker", Actor: null, At(23, 40), From: "Todo", To: "In Progress"))
            : new FakeLinearSideFacts();
    }

    // the golden linear-lookups/eng: ENG after a lookups tick at 23:58, with a member who left and two cycles
    private static LinearLookupsResponse Eng() => new(
        new LinearTeamRef("00000000-0000-4000-8000-0000000000e1", "ENG", "Engineering"),
        At(23, 58),
        [
            new LinearStateOption("00000000-0000-4000-8000-000000000054", "Backlog", "backlog", 0),
            new LinearStateOption("00000000-0000-4000-8000-000000000051", "Todo", "unstarted", 1),
            new LinearStateOption("00000000-0000-4000-8000-000000000052", "In Progress", "started", 2),
            new LinearStateOption("00000000-0000-4000-8000-000000000053", "In Review", "started", 3),
            new LinearStateOption("00000000-0000-4000-8000-000000000055", "Done", "completed", 4),
        ],
        [
            new LinearMemberOption("00000000-0000-4000-8000-0000000000a1", "Joi Example", Active: true),
            new LinearMemberOption("00000000-0000-4000-8000-0000000000a2", "Sam Lee", Active: true),
            new LinearMemberOption("00000000-0000-4000-8000-0000000000a3", "Former Member", Active: false),
        ],
        [new LinearLabelOption("00000000-0000-4000-8000-0000000000d1", "bug", "#eb5757"), new LinearLabelOption("00000000-0000-4000-8000-0000000000d2", "api", "#5e6ad2")],
        [
            new LinearCycleOption("00000000-0000-4000-8000-0000000000c1", 14, "Cycle 14", new DateTimeOffset(2025, 12, 29, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero), Current: true),
            new LinearCycleOption("00000000-0000-4000-8000-0000000000c2", 15, "Cycle 15", new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 26, 0, 0, 0, TimeSpan.Zero), Current: false),
        ]);

    private static DateTimeOffset At(int hour, int minute) => new(2025, 12, 31, hour, minute, 0, TimeSpan.Zero);
}
