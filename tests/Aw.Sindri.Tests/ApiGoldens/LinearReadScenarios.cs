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
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services => services.Replace(ServiceDescriptor.Singleton<ILinearSideFacts>(new FakeLinearSideFacts())));

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
}
