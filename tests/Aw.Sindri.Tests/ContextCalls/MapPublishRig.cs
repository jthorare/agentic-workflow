using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Extract;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// A started host over fixture repos for the map publish gate's integration cases (code index spec 6.4, 11): the real engine on real temp files, <see cref="ScriptedOutlooks"/> for the code index,
/// the host's <see cref="FakeTimeProvider"/> for the clock. A tick runs the 'context-map-refresh' service as the scheduler does and then waits for the jobs it started to end and the projector to catch up.
/// </summary>
internal sealed class MapPublishRig : IAsyncDisposable
{
    private MapPublishRig(GoldenHost host)
    {
        Host = host;
    }

    /// <summary>The code index's answers.</summary>
    internal ScriptedOutlooks Outlooks { get; private init; } = new();

    /// <summary>The host.</summary>
    internal GoldenHost Host { get; }

    /// <summary>The host's clock.</summary>
    internal FakeTimeProvider Clock => (FakeTimeProvider)Host.App.Services.GetRequiredService<TimeProvider>();

    private static int _moves;
    private static readonly string[] _identity = ["-c", "user.name=t", "-c", "user.email=t@example.test"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Starts a host mapping <paramref name="repos"/> (name, path) with the publish wait of <paramref name="waitMinutes"/>.</summary>
    /// <param name="repos">The mapped repos.</param>
    /// <param name="waitMinutes">'context.publish.waitForIndexMinutes'.</param>
    /// <param name="outlooks">The outlooks to answer with, or null for a new set.</param>
    /// <param name="home">A home of the caller's (which it removes), or null for a fresh one. A second start on the same home is a restart: the profile stays approved.</param>
    /// <param name="locks">The heavy-lock directory.</param>
    /// <param name="restart">True for a second start on <paramref name="home"/>.</param>
    /// <param name="idScope">The id scope of a restarted host.</param>
    /// <param name="extra">More service registrations (a decorated engine), or null.</param>
    /// <returns>The rig.</returns>
    internal static async Task<MapPublishRig> Start(IReadOnlyList<(string Name, string Path)> repos, int waitMinutes, string locks, ScriptedOutlooks? outlooks = null, string? home = null, uint idScope = 0, bool restart = false, Action<IServiceCollection>? extra = null)
    {
        var scripted = outlooks ?? new ScriptedOutlooks();
        var yaml = "schemaVersion: 1\nuser: golden\nrepos:\n" + string.Concat(repos.Select(static r => "  - { name: " + r.Name + ", path: \"" + r.Path + "\" }\n"))
            + "context:\n  docs: { globs: [\"docs/**/*.md\"] }\n  sessions: { enabled: true }\n  publish: { waitForIndexMinutes: "
            + waitMinutes.ToString(CultureInfo.InvariantCulture) + " }\n";
        var host = await GoldenHost.Start(temporary: true, Ct, yaml, extra: services =>
        {
            // why: graphify and the box's heavy-job lock would stall the build under load; the outlooks stand for the code index
            _ = services.RemoveAll<IClusterSource>();
            _ = services.AddSingleton<IClusterSource>(new FakeClusterSource());
            _ = services.RemoveAll<HeavyLockSettings>();
            _ = services.AddSingleton(new HeavyLockSettings(Path.Combine(locks, "heavy-job.lock"), 2, Held: false));
            _ = services.RemoveAll<IIndexOutlooks>();
            _ = services.AddSingleton<IIndexOutlooks>(scripted);
            extra?.Invoke(services);
        }, home: home, idScope: idScope);
        var rig = new MapPublishRig(host) { Outlooks = scripted };
        if (restart)
        {
            return rig;
        }

        var hash = (await host.LiveHash(Ct)).Value;
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)));
        Assert.Equal(System.Net.HttpStatusCode.OK, approved.Status);
        Assert.True((await rig.Run("context-docs-refresh")).IsSuccess);
        return rig;
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    /// <summary>Runs the named scheduled service once, as the scheduler enters it.</summary>
    internal async Task<Kernel.Result<ScheduledResult>> Run(string service)
    {
        await using var scope = Host.App.Services.CreateAsyncScope();
        var run = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == service);
        var grant = Host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        using var caller = Host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService(service);
        return await run.Run(new ScheduledRun(grant, Clock.GetUtcNow()), Ct);
    }

    /// <summary>One tick of the 'context-map-refresh' service, then every job it started ends.</summary>
    internal async Task<Kernel.Result<ScheduledResult>> Tick()
    {
        var before = Count(await Events(), EventTypes.JobFinished);
        var started = Count(await Events(), EventTypes.JobStarted);
        var result = await Run("context-map-refresh");
        await Settle(before, started);
        return result;
    }

    /// <summary>A human build of the draft ('context-rebuild' as the review tests run it), settled.</summary>
    internal async Task<Kernel.Result<ScheduledResult>> Build()
    {
        var result = await Run("context-rebuild");
        await Project();
        return result;
    }

    /// <summary>Waits until every job that started has finished (job events over the jobs the run began), then catches the views up.</summary>
    private async Task Settle(int finishedBefore, int startedBefore)
    {
        _ = finishedBefore;
        for (var i = 0; i < 480; i++)
        {
            var events = await Events();
            if (Count(events, EventTypes.JobFinished) >= Count(events, EventTypes.JobStarted) && Count(events, EventTypes.JobStarted) >= startedBefore)
            {
                await Project();
                return;
            }

            await Task.Delay(250, Ct);
        }

        Assert.Fail("the jobs didn't end");
    }

    /// <summary>The projector catches the views up to the ledger.</summary>
    internal async Task Project() => _ = await Host.App.Services.GetRequiredService<Projector>().CatchUp(Ct);

    /// <summary>The ledger.</summary>
    internal async Task<IReadOnlyList<LedgerEvent>> Events() =>
        (await Host.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 10_000, Ct)).Value;

    /// <summary>The ledger events of one type, oldest first.</summary>
    internal async Task<IReadOnlyList<LedgerEvent>> Of(string type) => [.. (await Events()).Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal))];

    /// <summary>The payload of <paramref name="event"/> as JSON.</summary>
    internal static JsonElement Payload(LedgerEvent @event) => JsonDocument.Parse(@event.Envelope.Payload).RootElement.Clone();

    private static int Count(IReadOnlyList<LedgerEvent> events, string type) => events.Count(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal));

    /// <summary>The stored publish state, read as the service reads it.</summary>
    internal async Task<MapPublishState> State()
    {
        await using var scope = Host.App.Services.CreateAsyncScope();
        using var caller = Host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("context-map-refresh");
        var scopes = scope.ServiceProvider.GetRequiredService<ContextScopes>();
        var current = await scopes.Current(Ct);
        return (await Host.App.Services.GetRequiredService<IContextEngine>().Held(current.Value, Ct)).Value;
    }

    /// <summary>Accepts every deterministic proposal of the draft, as a person at the desk does.</summary>
    internal async Task AcceptDeterministic()
    {
        var accepted = await Host.Http.Ask(Post("/sindri/context/map/review", """{"verb":"accept-deterministic"}""", [.. Human(Host.Token.Value, client: "ui").Where(h => h.Name != SindriHeaders.Terminal)]));
        Assert.Equal(System.Net.HttpStatusCode.OK, accepted.Status);
    }

    /// <summary>A scoping run proposes a surface onto the latest draft: an entry only a person can decide (the cleanup pass leaves it), so a promote is refused with SND-CONTEXT-046 until then.</summary>
    /// <param name="repo">The mapped repo's profile name.</param>
    /// <param name="run">The scoping run's name; a run adds its own surface.</param>
    internal async Task Propose(string repo, int run)
    {
        var body = "{\"run\":\"scope-" + run.ToString(CultureInfo.InvariantCulture) + "\",\"repo\":\"" + repo + "\",\"surfaces\":[{\"name\":\"Zoom " + run.ToString(CultureInfo.InvariantCulture) + "\",\"kind\":\"feature\",\"evidence\":[]}]}";
        var proposed = await Host.Http.Ask(Post("/sindri/context/map/proposals", body, [.. Human(Host.Token.Value, client: "ui").Where(h => h.Name != SindriHeaders.Terminal)]));
        Assert.True(proposed.Status == System.Net.HttpStatusCode.OK, proposed.Body);
    }

    /// <summary>A person promotes the latest draft at the terminal (the typed confirmation).</summary>
    internal async Task HumanPromote()
    {
        var preview = await Host.Http.Ask(Post("/sindri/context/map/promote", "{}", Human(Host.Token.Value)));
        Assert.True(preview.Status == System.Net.HttpStatusCode.OK, preview.Body);
        var shown = JsonDocument.Parse(preview.Body).RootElement;
        var draft = shown.GetProperty("draft").GetString();
        var challenge = shown.GetProperty("challenge");
        var body = JsonSerializer.Serialize(new { draft, answer = new { challenge = challenge.GetProperty("challenge").GetString(), typed = challenge.GetProperty("action").GetString() } });
        var promoted = await Host.Http.Ask(Post("/sindri/context/map/promote", body, Human(Host.Token.Value)));
        Assert.True(promoted.Status == System.Net.HttpStatusCode.OK, promoted.Body);
    }

    /// <summary>The first current map: a human build, the deterministic proposals accepted, a human promote.</summary>
    internal async Task FirstMap()
    {
        Assert.True((await Build()).IsSuccess);
        await AcceptDeterministic();
        await HumanPromote();
    }

    /// <summary>Commits a new command id to <paramref name="repo"/>'s checkout, which moves its trunk and gives the next build a new deterministic proposal for a person to decide.</summary>
    /// <param name="repo">The checkout.</param>
    /// <returns>The new head.</returns>
    internal static async Task<string> Commit(string repo)
    {
        var id = "moved-" + Interlocked.Increment(ref _moves).ToString(CultureInfo.InvariantCulture);
        var folder = Path.Combine(repo, "src", "moves");
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, id + ".ts"), "export type MovedCommandId = \"" + id + "\";\n", Ct);
        _ = await Git(repo, "add", ".");
        _ = await Git(repo, "commit", "-m", "move trunk: " + id);
        return (await Git(repo, "rev-parse", "HEAD")).Trim();
    }

    /// <summary>The checkout's head.</summary>
    internal static async Task<string> Head(string repo) => (await Git(repo, "rev-parse", "HEAD")).Trim();

    private static async Task<string> Git(string repo, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = repo };
        foreach (var argument in _identity.Concat(arguments))
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct);
        Assert.Equal(0, process.ExitCode);
        return output;
    }
}
