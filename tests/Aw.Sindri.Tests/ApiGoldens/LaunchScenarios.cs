using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P10a's goldens for the open and the launch it asks for (UI spec E-1): 'POST /items/{id}/open' through the API, and 'GET /launches/{id}' over launches
/// the scenario appends itself (a launcher service isn't running, so a started or failed outcome is appended as it would write it). Every scenario starts on
/// <see cref="ItemScenarios.ProfileYaml"/> (two repos), approves it and makes its item through the API; the budget ones add a budget to the profile and
/// a spend reader with set figures. Ids are sequential.
/// </summary>
internal static class LaunchScenarios
{
    private const string Budget = "agents:\n  claude-code: { dailyBudget: 10 }\n";
    private const string NoSuchLaunch = "LCH-000000000000000000000000000000ff";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("items-id-open", "requested"), ("items-id-open", "in-progress-008"), ("items-id-open", "budget-hard-010"), ("items-id-open", "budget-soft-warning"),
        ("items-id-open", "bad-target"), ("items-id-open", "closed-item"), ("items-id-open", "attach-no-session-006"), ("items-id-open", "refused-session"),
        ("items-id-open", "refused-origin"),
        ("launches-id", "requested"), ("launches-id", "started-opened"), ("launches-id", "started-unconfirmed"), ("launches-id", "started-late-attach"),
        ("launches-id", "started-viewer-failed"), ("launches-id", "started-print"), ("launches-id", "failed-host-stage"), ("launches-id", "failed-expired-007"),
        ("launches-id", "not-found"), ("launches-id", "bad-id"), ("launches-id", "session-other-repo"),
        .. LaunchTargetScenarios.All,
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>The live profile the scenario's host starts with.</summary>
    public static string ProfileYaml(string scenario) => scenario.StartsWith("budget-", StringComparison.Ordinal) ? ItemScenarios.ProfileYaml + Budget : ItemScenarios.ProfileYaml;

    /// <summary>Starts the scenario's host: the budget ones read a spend of 10 (hard) or 8 (soft) for claude-code.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct)
    {
        if (LaunchTargetScenarios.NeedsOwnHost(scenario))
        {
            return LaunchTargetScenarios.Start(scenario, ct);
        }

        var spend = new FixedLaunchSpend();
        if (scenario.StartsWith("budget-", StringComparison.Ordinal))
        {
            spend.Spent(AgentId.From("claude-code"), scenario == "budget-hard-010" ? 10m : 8m);
        }

        return GoldenHost.Start(temporary: true, ct, ProfileYaml(scenario), extra: services => services.Replace(ServiceDescriptor.Singleton<ILaunchSpend>(spend)));
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        if (slug.StartsWith("launch-targets", StringComparison.Ordinal))
        {
            return await LaunchTargetScenarios.Body(host, slug, scenario, ct);
        }

        await ViewScenarios.Approve(host, ct);
        var item = await ItemScenarios.Create(host, "Try the queue", "demo");
        await ViewScenarios.CatchUp(host, ct);
        return slug == "items-id-open" ? await Open(host, item, scenario, ct) : await Launch(host, item, scenario, ct);
    }

    private static async Task<string> Open(GoldenHost host, string item, string scenario, CancellationToken ct)
    {
        var path = $"/sindri/items/{item}/open";
        var human = ItemScenarios.Human(host);
        switch (scenario)
        {
            case "in-progress-008":
                _ = await Send(host, Post(path, Body(), human));
                await ViewScenarios.CatchUp(host, ct);
                return await Send(host, Post(path, Body(), human));
            case "bad-target":
                return await Send(host, Post(path, Body(target: "Not An Agent!"), human));
            case "closed-item":
                _ = await Send(host, Post($"/sindri/items/{item}/close", """{"reason":"done","itemVersion":1}""", human));
                await ViewScenarios.CatchUp(host, ct);
                return await Send(host, Post(path, Body(version: 2), human));
            case "attach-no-session-006":
                return await Send(host, Post(path, Body(mode: "attach"), human));
            case "refused-session":
                return await Send(host, Post(path, Body(), ItemScenarios.Session("demo")));
            case "refused-origin":
                return await Send(host, Post(path, Body(), [.. human, ("Origin", "http://localhost:3000")]));
            default:
                return await Send(host, Post(path, Body(), human));   // 'requested', and the two budget ones
        }
    }

    private static string Body(string mode = "open", string? target = null, long version = 1)
    {
        var json = new System.Text.Json.Nodes.JsonObject { ["mode"] = mode, ["itemVersion"] = version };
        if (target is not null)
        {
            json["target"] = target;
        }

        return json.ToJsonString();
    }

    private static async Task<string> Launch(GoldenHost host, string item, string scenario, CancellationToken ct)
    {
        var launch = A.FirstLaunchId;
        var human = ItemScenarios.Human(host);
        switch (scenario)
        {
            case "not-found":
                return await Send(host, Get($"/sindri/launches/{NoSuchLaunch}", human));
            case "bad-id":
                return await Send(host, Get("/sindri/launches/not-a-launch", human));
            default:
                break;
        }

        await Seed(host, launch, item, scenario, ct);
        await ViewScenarios.CatchUp(host, ct);
        return scenario == "session-other-repo"
            ? await Send(host, Get($"/sindri/launches/{launch.Value}", ItemScenarios.Session("other")))
            : await Send(host, Get($"/sindri/launches/{launch.Value}", human));
    }

    private static async Task Seed(GoldenHost host, LaunchId launch, string item, string scenario, CancellationToken ct)
    {
        await Request(host, launch, ItemId.From(item), ct);
        switch (scenario)
        {
            case "started-opened":
                await Started(host, launch, item, ViewerOutcomeKind.Opened, ct);
                break;
            case "started-unconfirmed":
                await Started(host, launch, item, ViewerOutcomeKind.Unconfirmed, ct);
                break;
            case "started-late-attach":
                await Started(host, launch, item, ViewerOutcomeKind.Unconfirmed, ct);
                await ViewScenarios.Append(host, EventTypes.LaunchViewerAttached, new LaunchViewerAttached(launch, host.Now()), $"launch.viewer-attached:{launch.Value}", ct);
                break;
            case "started-viewer-failed":
                await Started(host, launch, item, ViewerOutcomeKind.Failed, ct, new LaunchViewer("exec-template", "kitty", ViewerOutcomeKind.Failed, "SND-LAUNCH-001", "kitty: no display"));
                break;
            case "started-print":
                await Started(host, launch, item, ViewerOutcomeKind.Skipped, ct, new LaunchViewer("print", "print", ViewerOutcomeKind.Skipped, Code: null, Detail: null));
                break;
            case "failed-host-stage":
                await Failed(host, launch, item, "SND-LAUNCH-009", "The launch timed out (fix: run `sindri doctor`)", LaunchStage.Host, ct);
                break;
            case "failed-expired-007":
                await Failed(host, launch, item, "SND-LAUNCH-007", "The launch was interrupted (fix: open it again)", stage: null, ct);
                break;
            default:
                break;   // 'requested', 'session-other-repo'
        }
    }

    internal static Task Request(GoldenHost host, LaunchId launch, ItemId item, CancellationToken ct) =>
        ViewScenarios.Append(host, EventTypes.ItemOpenRequested, new ItemOpenRequested(launch, item, LaunchMode.Open, AgentId.From("claude-code"), "click"), $"item.open-requested:{launch.Value}", ct);

    internal static Task Started(GoldenHost host, LaunchId launch, string item, ViewerOutcomeKind outcome, CancellationToken ct, LaunchViewer? viewer = null) =>
        ViewScenarios.Append(host, EventTypes.LaunchStarted,
            new LaunchStarted(launch, ItemId.From(item), LaunchMode.Open, AgentId.From("claude-code"), SessionId.From("0a1b2c3d-0000-4000-8000-000000000001"), "sindri " + item,
                viewer ?? new LaunchViewer("warp", "Warp", outcome, Code: null, Detail: null), ["tmux", "-L", "sindri", "attach", "-t", "=snd-" + launch.Value], CatalogSource.Builtin, "sha256:" + new string('a', 64)),
            $"launch.outcome:{launch.Value}", ct);

    private static Task Failed(GoldenHost host, LaunchId launch, string item, string code, string message, LaunchStage? stage, CancellationToken ct) =>
        ViewScenarios.Append(host, EventTypes.LaunchFailed, new LaunchFailed(launch, ItemId.From(item), code, message, stage), $"launch.outcome:{launch.Value}", ct);

    private static async Task<string> Send(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
