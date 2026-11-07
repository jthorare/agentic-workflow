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
        ("linear-lookups", "session-refused"),
        ("linear-since", "empty"), ("linear-since", "session-refused"),
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
            ("linear-lookups", _) => "/sindri/linear/lookups/ENG",
            ("linear-since", "empty") => "/sindri/linear/since?after=lin_000000000000003f",
            _ => "/sindri/linear/since",
        };
        return (await host.Http.Ask(Get(path, caller))).Body;
    }

    /// <summary>
    /// The facts a scenario's host answers with. 'linear-since/three' (still pending in <see cref="LinearScenarios"/> until its golden is re-recorded with the real marker) is Sam Lee's assignment of ENG-31, a mention
    /// on ENG-12 and ENG-12's move from Todo to In Progress; Context keeps no actor for an assignment or a state move, so only the mention has one, as the real adapter answers.
    /// </summary>
    internal static FakeLinearSideFacts Facts(string slug, string scenario)
    {
        var sam = new LinearPerson("00000000-0000-4000-8000-0000000000a2", "Sam Lee");
        return (slug, scenario) is ("linear-since", "three")
            ? new FakeLinearSideFacts().WithItems(
                new LinearSinceItem("assigned", "ENG-31", "Export limits: cap the CSV at 50k rows", Actor: null, At(22, 10), From: null, To: null),
                new LinearSinceItem("mentioned", "ENG-12", "Retry budget for the sync worker", sam, At(23, 30), From: null, To: null),
                new LinearSinceItem("state", "ENG-12", "Retry budget for the sync worker", Actor: null, At(23, 40), From: "Todo", To: "In Progress"))
            : new FakeLinearSideFacts();
    }

    private static DateTimeOffset At(int hour, int minute) => new(2025, 12, 31, hour, minute, 0, TimeSpan.Zero);
}
