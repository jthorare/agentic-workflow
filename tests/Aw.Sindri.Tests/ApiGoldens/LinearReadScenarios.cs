using Aw.Sindri.Core.Linear;
using Aw.Sindri.Tests.LinearReads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Linear hub's side reads (#2077 Ph3 1a; Linear hub spec 2026-11-04 §3, §4.2, §4.3; contract AGREED by api and ui): 'GET /linear/lookups/{team}' and 'GET /linear/since'. The port behind them
/// (<see cref="ILinearSideFacts"/>) is a <see cref="FakeLinearSideFacts"/> seeded with what Context's adapter must answer for the golden: for the strip Sam Lee's assignment of ENG-31, a mention on ENG-12 and ENG-12's move from Todo to In Progress. The clock stands at the epoch.
/// </summary>
internal static class LinearReadScenarios
{
    private const string Sam = "00000000-0000-4000-8000-0000000000a2";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-lookups", "session-refused"),
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
            ("linear-lookups", _) => "/sindri/linear/lookups/ENG",
            ("linear-since", "empty") => "/sindri/linear/since?after=lin_000000000000003f",
            _ => "/sindri/linear/since",
        };
        return (await host.Http.Ask(Get(path, caller))).Body;
    }

    private static FakeLinearSideFacts Facts(string slug, string scenario) => (slug, scenario) switch
    {
        ("linear-since", "three") => new FakeLinearSideFacts().WithItems(
            Item("assigned", "ENG-31", "Export limits: cap the CSV at 50k rows", At(22, 10), from: null, to: null),
            Item("mentioned", "ENG-12", "Retry budget for the sync worker", At(23, 30), from: null, to: null),
            Item("state", "ENG-12", "Retry budget for the sync worker", At(23, 40), from: "Todo", to: "In Progress")),
        _ => new FakeLinearSideFacts(),
    };

    private static DateTimeOffset At(int hour, int minute) => new(2025, 12, 31, hour, minute, 0, TimeSpan.Zero);

    private static LinearSinceItem Item(string kind, string key, string title, DateTimeOffset at, string? from, string? to) => new(kind, key, title, new LinearPerson(Sam, "Sam Lee"), at, from, to);
}
