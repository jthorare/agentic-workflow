using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Commands.Authorization;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>
/// The golden host with the job routes' fixtures (API P6 unit G): a service 'stuck' whose runs wait for the test, and the echo job registered through
/// AddJob and mapped through MapJob at the test-only route POST /sindri/test/echo. The route, its catalog row and its JSON types live only in this
/// host, never in the production one (S10).
/// </summary>
internal static class JobHost
{
    /// <summary>Two repos (a session can sit outside a job's repo), the scheduled 'heartbeat' and 'stuck', and 'phantom', a profile service this build lacks.</summary>
    public const string ProfileYaml = "schemaVersion: 1\nuser: golden\nrepos:\n  - { name: demo, path: /work/demo }\n  - { name: other, path: /work/other }\nservices:\n  heartbeat: { every: 5s }\n  stuck: { every: 5s }\n  phantom: { every: 5s }\n";

    /// <summary>An echo with this delay or more is keyed by its line count, so several can run at once (the cap golden); the others share the key 'echo'.</summary>
    public const int LongDelayMs = 1_000;

    /// <summary>The test-only route.</summary>
    public const string EchoRoute = "/sindri/test/echo";

    /// <summary>Starts the host.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <param name="home">An existing home (a restart), or null.</param>
    /// <param name="idScope">The scope of the ids; a restart takes another.</param>
    /// <param name="pump">Whether the views follow the ledger by themselves (the goldens drive the projector step by step instead).</param>
    /// <returns>The started host.</returns>
    /// <param name="more">More registrations (a test replaces a port with its fake), made after the host's own.</param>
    public static async Task<GoldenHost> Start(CancellationToken ct, string? home = null, uint idScope = 0, bool pump = true, Action<IServiceCollection>? more = null)
    {
        var host = await GoldenHost.Start(temporary: true, ct, ProfileYaml, extra: services => { Register(services); more?.Invoke(services); }, map: Map, home: home, idScope: idScope);
        if (pump)
        {
            host.Own(new ViewsPump(host));
        }

        return host;
    }

    /// <summary>The human's headers on <paramref name="host"/>.</summary>
    /// <param name="host">The host.</param>
    /// <returns>The headers.</returns>
    public static (string Name, string Value)[] Human(GoldenHost host) => CallerRequests.Human(host.Token.Value);

    /// <summary>Starts an echo job as the human.</summary>
    /// <param name="host">The host.</param>
    /// <param name="lines">How many lines it writes.</param>
    /// <param name="delayMs">The pause after each, on the host's fake clock: nothing advances it, so the job writes one line and runs until it is cancelled.</param>
    /// <returns>The job and the sequence of its start.</returns>
    public static async Task<(JobId Job, long Sequence)> StartEcho(GoldenHost host, int lines, int delayMs)
    {
        var answer = await host.Http.Ask(Post(EchoRoute, string.Create(CultureInfo.InvariantCulture, $"{{\"lines\":{lines},\"delayMs\":{delayMs}}}"), Human(host)));
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var json = JsonDocument.Parse(answer.Body);
        return (JobId.From(json.RootElement.GetProperty("jobId").GetString()!), json.RootElement.GetProperty("accepted").GetProperty("sequence").GetInt64());   // why: a started job answers its id
    }

    /// <summary>A hang guard on the real clock for what a job or the views should do at once: the host's own clock is fake and would never fire it.</summary>
    public static readonly TimeSpan Hang = TimeSpan.FromSeconds(10);

    /// <summary>The hang guard for a job that does real work (a sources sync against a fake Onyx, a map build) to end: a loaded gate box ran one past <see cref="Hang"/>, so this one is longer but stays under the shortest test timeout (30 s) that waits on it, and its named failure still comes first.</summary>
    public static readonly TimeSpan SettleHang = TimeSpan.FromSeconds(20);

    /// <summary>Waits until every job this host started has ended and its last event is appended, then lets the views catch up. A job that does not end fails the wait by name after <see cref="SettleHang"/>.</summary>
    /// <param name="host">The host.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes then.</returns>
    public static async Task Settle(GoldenHost host, CancellationToken ct)
    {
        var runner = host.App.Services.GetRequiredService<JobRunner>();
        try
        {
            await runner.Idle().WaitAsync(SettleHang, TimeProvider.System, ct);
        }
        catch (TimeoutException e)
        {
            throw new TimeoutException($"jobs still running {SettleHang.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s after the test asked them to end: {string.Join(", ", runner.Active)}", e);
        }

        await ViewScenarios.CatchUp(host, ct);
    }

    /// <summary>
    /// Asks, and asks again while the views are behind, rebuilding or stalled on a pass that failed under load (503 SND-API-010, SND-API-011 or SND-VIEW-001,
    /// with a catch-up between the tries), for at most <see cref="Hang"/> of real time. Any other answer, and the last refusal, is returned as it is.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="ask">The request.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The answer.</returns>
    public static async Task<Answer> Retrying(GoldenHost host, Func<Task<Answer>> ask, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            var answer = await ask();
            if (answer.Status != HttpStatusCode.ServiceUnavailable || answer.Code is not ("SND-API-010" or "SND-API-011" or "SND-VIEW-001") || Stopwatch.GetElapsedTime(started) > Hang)
            {
                return answer;
            }

            await ViewScenarios.CatchUp(host, ct);
        }
    }

    /// <summary>Cancels a job as the human and fails at once, naming the answer, unless it is a 200.</summary>
    /// <param name="host">The host.</param>
    /// <param name="job">The job.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes then.</returns>
    public static async Task Cancel(GoldenHost host, JobId job, CancellationToken ct)
    {
        var answer = await Retrying(host, () => host.Http.Ask(Post($"/sindri/jobs/{job.Value}/cancel", json: null, Human(host))), ct);
        Assert.True(answer.Status == HttpStatusCode.OK, $"cancelling {job.Value} answered {answer.Status} {answer.Code}: {answer.Body}");
    }

    /// <summary>GETs <paramref name="path"/> as the human.</summary>
    /// <param name="host">The host.</param>
    /// <param name="path">The route.</param>
    /// <returns>The answer.</returns>
    public static Task<Answer> Read(GoldenHost host, string path) => host.Http.Ask(Get(path, Human(host)));

    /// <summary>Lets go of the lease, as a stop does, so another host can take this home.</summary>
    /// <param name="host">The host.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes then.</returns>
    public static async Task ReleaseLease(GoldenHost host, CancellationToken ct)
    {
        var held = host.App.Services.GetRequiredService<HeldLease>();
        var grant = held.Current ?? throw new InvalidOperationException("the host holds no lease");
        held.Clear();
        _ = (await host.App.Services.GetRequiredService<ILease>().Release(grant, ct)).Value;
    }

    private static void Register(IServiceCollection services)
    {
        var options = AwJson.CreateOptions(JsonTypeInfoResolver.Combine(SindriJsonContext.Default, EchoJsonContext.Default));
        var echo = Rows.Of(ActionClass.Job, "EchoJob", typeof(EchoJobRequest));
        _ = services
            .AddSingleton<BlockedService>()
            .AddSingleton<Core.Scheduling.IScheduledService>(static sp => sp.GetRequiredService<BlockedService>())
            .AddSingleton<EchoJobProbe>()
            .Replace(ServiceDescriptor.Singleton(options))
            .Replace(ServiceDescriptor.Singleton<ICommandCatalog>(new InMemoryCommandCatalog([.. new CommandCatalog().Rows, echo])))
            .AddJob<EchoJobRequest, EchoReport, EchoJobService>(static r => JobKey.From(r.DelayMs >= LongDelayMs ? string.Create(CultureInfo.InvariantCulture, $"echo-{r.Lines}") : "echo"), static r => string.Create(CultureInfo.InvariantCulture, $"echoed {r.Lines}"));
    }

    private static void Map(WebApplication app) =>
        _ = app.MapGroup("/sindri").MapGroup(string.Empty).WithMetadata(CommandRouteMetadata.Instance).MapJob<EchoJobRequest, EchoReport>("/test/echo");

    /// <summary>
    /// Keeps the views at the ledger's head while a test runs. A running job appends events by itself, and the golden host hosts no projector: a command
    /// that waits for the views (every command that reads them) would wait on the fake clock for ever.
    /// </summary>
    private sealed class ViewsPump : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();

        public ViewsPump(GoldenHost host)
        {
            _ = Task.Run(() => Pump(host, _stop.Token), CancellationToken.None);
        }

        public void Dispose() => _stop.Cancel();

        private static async Task Pump(GoldenHost host, CancellationToken stop)
        {
            var logged = false;
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await ViewScenarios.CatchUp(host, stop);
                    await Task.Delay(10, stop);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    return;
                }
#pragma warning disable CA1031 // a pass that fails (the host going away, a busy file) is tried again on the next beat: the pump never quits on its own
                catch (Exception e)
#pragma warning restore CA1031
                {
                    if (!logged)
                    {
                        logged = true;
                        await Console.Error.WriteLineAsync($"JobHost.ViewsPump: a catch-up failed, trying again: {e.GetType().Name}: {e.Message}");
                    }

                    await Task.Delay(10, CancellationToken.None);
                }
            }
        }
    }
}
