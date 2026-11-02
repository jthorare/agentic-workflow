using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Scheduling;

/// <summary>
/// A service that keeps running idle after an ok run records nothing (API spec C8), so its last recorded slot ages while
/// the scheduler ticks on; GET /sindri/services must still list the upcoming slot as nextSlot, not a slot long past.
/// The real host in a temp home with an idle service, the scheduler driven on the fake clock.
/// </summary>
public sealed class ListedNextSlotTests : IDisposable
{
    private static readonly ServiceName _watch = ServiceName.From("pr-watch");
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly IdleService _service = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Theory, Trait("Lane", "integration")]
    [InlineData("5s", 5)]
    [InlineData("1m", 60)]
    public async Task A_service_idling_after_its_last_recorded_run_lists_a_next_slot_that_is_still_to_come(string every, int seconds)
    {
        var interval = TimeSpan.FromSeconds(seconds);
        await using var app = await Start(every);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
        var clock = new SteppedClock(_clock);
        using var scheduler = ActivatorUtilities.CreateInstance<SchedulerHost>(app.Services, clock);
        await scheduler.StartAsync(Token);

        await StepUntil(clock, interval, runs: 8);   // the first run is recorded, the idle ones after it are not
        Assert.Equal(ProjectorPass.CaughtUp, await app.Services.GetRequiredService<Projector>().CatchUp(Token));
        var listed = await Listed(http);
        await scheduler.StopAsync(Token);
        await app.StopAsync(Token);

        var now = _clock.GetUtcNow();
        Assert.True(listed.NextSlot > now, string.Create(CultureInfo.InvariantCulture, $"next {listed.NextSlot:O} is not after now {now:O}"));
        Assert.True(listed.NextSlot <= now + interval, string.Create(CultureInfo.InvariantCulture, $"next {listed.NextSlot:O} is more than one cadence after now {now:O}"));
    }

    private async Task<WebApplication> Start(string every)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), $"schemaVersion: 1\nuser: idle\nservices:\n  pr-watch: {{ every: {every} }}\n", Token);
        var app = LoopbackBinding.Build([], n => n == "AW_SINDRI_HOME" ? _home.Path : null, _clock, services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IIdGenerator>(new SequentialIdGenerator()));
            _ = services.Replace(ServiceDescriptor.Singleton<IProcessProbe>(new FakeProcessProbe()));
            foreach (var own in services.Where(d => d.ServiceType == typeof(IScheduledService) && d.ImplementationType == typeof(PrWatchService)).ToList())
            {
                _ = services.Remove(own);   // the host's own pr-watch would run in place of the idle one (same name)
            }

            _ = services.AddSingleton<IScheduledService>(_service);
            NoTemporal.Use(services);
        });
        using (app.Services.GetRequiredService<ICallerContextAccessor>().EnterService("host"))   // holds the lease, as serve does
        {
            await using var scope = app.Services.CreateAsyncScope();
            _ = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>()
                .Handle(new StartProcessRequest(), Token)).Value;
        }

        var live = (await app.Services.GetRequiredService<IProfileSource>().Load(Token)).Value;
        _ = (await app.Services.GetRequiredService<IApprovals>().Save(live, Token)).Value;
        _ = Pipeline.Configure(app);
        await app.StartAsync(Token);
        return app;
    }

    /// <summary>Steps the clock one cadence at a time until the service has run <paramref name="runs"/> times.</summary>
    private async Task StepUntil(SteppedClock clock, TimeSpan interval, int runs)
    {
        for (var step = 0; _service.Runs < runs; step++)
        {
            Assert.True(step < 60, "the service didn't run often enough within 60 cadences");
            await clock.Step(interval);
        }

        await clock.Armed();   // the run's outcome is settled before the loop arms its next wait
    }

    private static async Task<ServiceView> Listed(HttpClient http)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/sindri/services", UriKind.Relative)).AsSession();
        using var answer = await http.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var body = await answer.Content.ReadFromJsonAsync(HttpJson.Info<ListServicesResponse>(), Token);
        return Assert.Single(body!.Services, s => s.Name == _watch);
    }

    /// <summary>A service named 'pr-watch' that finds no work on any run.</summary>
    private sealed class IdleService : IScheduledService
    {
        private int _runs;

        public int Runs => Volatile.Read(ref _runs);

        public ServiceName Identity() => _watch;

        public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _runs);
            return ValueTask.FromResult<Result<ScheduledResult>>(ScheduledResult.Quiet);
        }
    }
}
