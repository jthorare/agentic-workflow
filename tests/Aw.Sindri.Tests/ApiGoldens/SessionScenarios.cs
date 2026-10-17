using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P10a's goldens for 'session wrap''s two calls (API spec §8.1): 'POST /sessions/{launchId}/heartbeat' and '/exit', each proven by the launch's own
/// proof in X-Sindri-Launch-Secret. The scenario starts a launch the way the launcher would (an open request, a started outcome and the hash file) and
/// calls as the session the wrap runs in.
/// </summary>
internal static class SessionScenarios
{
    /// <summary>The proof the launch's hash file holds.</summary>
    internal const string Proof = "golden-launch-proof";

    /// <summary>The session id the 'with-session-id' heartbeat carries.</summary>
    internal const string ReportedId = "0a1b2c3d-0000-4000-8000-000000000001";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sessions-id-heartbeat", "ok"), ("sessions-id-heartbeat", "with-session-id"), ("sessions-id-heartbeat", "bad-secret"),
        ("sessions-id-exit", "ok"), ("sessions-id-exit", "bad-secret"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>The path of the launch's route.</summary>
    internal static string Route(LaunchId launch, string call) => $"/sindri/sessions/{launch.Value}/{call}";

    /// <summary>The caller headers 'session wrap' sends, with the launch's proof.</summary>
    internal static (string Name, string Value)[] Wrap(string proof) => [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (SindriHeaders.LaunchSecret, proof)];

    /// <summary>Starts a launch of a new item as the launcher would, with its hash file written.</summary>
    internal static async Task<LaunchId> StartLaunch(GoldenHost host, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        var item = await ItemScenarios.Create(host, "Try the queue", "demo");
        await ViewScenarios.CatchUp(host, ct);
        var launch = A.FirstLaunchId;
        await LaunchScenarios.Request(host, launch, ItemId.From(item), ct);
        await LaunchScenarios.Started(host, launch, item, Core.Launch.ViewerOutcomeKind.Skipped, ct);
        await ViewScenarios.CatchUp(host, ct);
        LaunchSecrets.Write(new LauncherOptions(host.Home).SecretsDirectory, launch, Proof);
        return launch;
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        var launch = await StartLaunch(host, ct);
        var proof = scenario == "bad-secret" ? "not-the-proof" : Proof;
        var request = slug == "sessions-id-heartbeat"
            ? Post(Route(launch, "heartbeat"), scenario == "with-session-id" ? $$"""{"sessionId":"{{ReportedId}}"}""" : "{}", Wrap(proof))
            : Post(Route(launch, "exit"), """{"exitCode":7}""", Wrap(proof));
        return (await host.Http.Ask(request)).Body;
    }
}
