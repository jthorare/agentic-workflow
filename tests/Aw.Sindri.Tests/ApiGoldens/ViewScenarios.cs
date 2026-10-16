using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P2's golden scenarios (plan §6): the services and profile views, 'views rebuild' and the status rows that carry
/// views and health. The projector is never hosted in a golden host, so each scenario appends its events and drives
/// <see cref="Projector.CatchUp"/> itself on the fake clock; a stall comes from a 'service.ran' with an unparseable
/// payload (UI spec §18.2's trigger, no seam).
/// </summary>
internal static class ViewScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("services", "unapproved"), ("services", "approved"), ("services", "ran-ok"), ("services", "ran-failed"),
        ("services", "rebuilding"), ("services", "stalled"), ("services", "after-not-reached"), ("services", "after-malformed"),
        ("profile", "unapproved"), ("profile", "approved"), ("profile", "changed"), ("profile", "stalled"),
        ("views-rebuild", "accepted"), ("views-rebuild", "accepted-while-stalled"), ("views-rebuild", "refused-session"), ("views-rebuild", "refused-origin"),
        ("status", "views-stalled"), ("status", "views-rebuilding"), ("status", "service-failed"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        var token = host.Token.Value;
        var human = Human(token);
        return (slug, scenario) switch
        {
            ("services", "unapproved") => await Views(host, "/sindri/services", ct),
            ("services", "approved") => await Approved(host, "/sindri/services", ct),
            ("services", "ran-ok") => await Ran(host, ServiceOutcome.Ok, code: null, ct),
            ("services", "ran-failed") => await Ran(host, ServiceOutcome.Failed, "SND-SCHED-001", ct),
            ("services", "rebuilding") => await Rebuilding(host, "/sindri/services", ct),
            ("services", "stalled") => await Stalled(host, "/sindri/services", ct),
            ("services", "after-not-reached") => await NotReached(host, ct),
            ("services", "after-malformed") => await Ask(host, Get("/sindri/services", [.. human, (SindriHeaders.After, "soon")])),
            ("profile", "unapproved") => await Views(host, "/sindri/profile", ct),
            ("profile", "approved") => await Approved(host, "/sindri/profile", ct),
            ("profile", "changed") => await Changed(host, ct),
            ("profile", "stalled") => await Stalled(host, "/sindri/profile", ct),
            ("views-rebuild", "accepted") => await Ask(host, Post("/sindri/views/rebuild", json: null, human)),
            ("views-rebuild", "accepted-while-stalled") => await StalledThen(host, Post("/sindri/views/rebuild", json: null, human), ct),
            ("views-rebuild", "refused-session") => await Ask(host, Post("/sindri/views/rebuild", json: null, Session)),
            ("views-rebuild", "refused-origin") => await Ask(host, Post("/sindri/views/rebuild", json: null, [.. human, ("Origin", "http://localhost:3000")])),
            ("status", "views-stalled") => await Stalled(host, "/sindri/status", ct),
            ("status", "views-rebuilding") => await Rebuilding(host, "/sindri/status", ct),
            ("status", "service-failed") => await Ran(host, ServiceOutcome.Failed, "SND-SCHED-001", ct, "/sindri/status"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such view scenario"),
        };
    }

    private static (string Name, string Value)[] Session => [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")];

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;

    internal static async Task<string> Read(GoldenHost host, string path) => await Ask(host, Get(path, Human(host.Token.Value)));

    private static async Task<string> Views(GoldenHost host, string path, CancellationToken ct)
    {
        await CatchUp(host, ct);
        return await Read(host, path);
    }

    private static async Task<string> Approved(GoldenHost host, string path, CancellationToken ct)
    {
        await Approve(host, ct);
        await CatchUp(host, ct);
        return await Read(host, path);
    }

    private static async Task<string> Ran(GoldenHost host, ServiceOutcome outcome, string? code, CancellationToken ct, string path = "/sindri/services")
    {
        await Approve(host, ct);
        await Append(host, EventTypes.ServiceRan, new ServiceRan(CommandId.From(new Guid(0xa1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)), ServiceName.From("heartbeat"), KnownInstants.Epoch, outcome, code), "service.ran:golden", ct);
        await CatchUp(host, ct);
        return await Read(host, path);
    }

    private static async Task<string> Changed(GoldenHost host, CancellationToken ct)
    {
        await Approve(host, ct);
        await Drift(host, GoldenHost.ProfileYaml, ct);
        return await Read(host, "/sindri/profile");
    }

    /// <summary>Edits the live profile (a second service on top of <paramref name="baseYaml"/>), checks it, and lets the views catch up: the approved profile has drifted.</summary>
    internal static async Task Drift(GoldenHost host, string baseYaml, CancellationToken ct)
    {
        await File.WriteAllTextAsync(Path.Combine(host.Home, "profile", "profile.yaml"), baseYaml + "  scorer: { every: 1h }\n", ct);
        await using (var scope = Service(host, out var grant))
        {
            _ = (await scope.ServiceProvider.GetRequiredService<IService<CheckProfileRequest, CheckProfileResponse>>().Handle(new CheckProfileRequest(grant), ct)).Value;
        }

        await CatchUp(host, ct);
    }

    private static async Task<string> Rebuilding(GoldenHost host, string path, CancellationToken ct)
    {
        await Approve(host, ct);
        await CatchUp(host, ct);
        host.App.Services.GetRequiredService<IProjectorControl>().RequestRebuild();
        return await Read(host, path);
    }

    private static async Task<string> Stalled(GoldenHost host, string path, CancellationToken ct)
    {
        await Stall(host, ct);
        return await Read(host, path);
    }

    private static async Task<string> StalledThen(GoldenHost host, HttpRequestMessage request, CancellationToken ct)
    {
        await Stall(host, ct);
        return await Ask(host, request);
    }

    private static async Task<string> NotReached(GoldenHost host, CancellationToken ct)
    {
        await CatchUp(host, ct);
        var clock = (FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>();
        var pending = Ask(host, Get("/sindri/services", [.. Human(host.Token.Value), (SindriHeaders.After, "99")]));
        while (!pending.IsCompleted)   // the query waits 2 s on the fake clock: move it until the wait gives up
        {
            await Task.Delay(20, ct);
            clock.Advance(TimeSpan.FromSeconds(3));
        }

        return await pending;
    }

    private static async Task Stall(GoldenHost host, CancellationToken ct)
    {
        await CatchUp(host, ct);   // the first build finishes, so the stall is one of a running projector
        await Append(host, EventTypes.ServiceRan, "not a service run", "service.ran:unreadable", ct);
        await CatchUp(host, ct);
    }

    internal static async Task Approve(GoldenHost host, CancellationToken ct)
    {
        var hash = (await host.LiveHash(ct)).Value;
        var setup = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)));
        if (setup.Status != System.Net.HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"the setup approval failed: {setup.Status} {setup.Body}");
        }
    }

    internal static async Task CatchUp(GoldenHost host, CancellationToken ct) =>
        _ = await host.App.Services.GetRequiredService<Projector>().CatchUp(ct);

    internal static async Task Append<TPayload>(GoldenHost host, string type, TPayload payload, string key, CancellationToken ct)
    {
        await using var scope = Service(host, out var grant);
        var events = scope.ServiceProvider.GetRequiredService<EventFactory>();
        var appended = await scope.ServiceProvider.GetRequiredService<ILedgerStore>().Append(events.Entry(type, payload, key), grant, ct);
        _ = appended.Value;
    }

    /// <summary>A scope entered as the caller 'service:host', with the lease grant the host holds.</summary>
    internal static AsyncServiceScope Service(GoldenHost host, out LeaseGrant grant)
    {
        grant = host.App.Services.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the host holds no lease");
        _ = host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("host");
        return host.App.Services.CreateAsyncScope();
    }
}
