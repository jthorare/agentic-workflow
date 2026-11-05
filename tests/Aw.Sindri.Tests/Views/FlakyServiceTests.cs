using System.Net;
using System.Net.Http.Json;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// Phase signal 4 (API spec P2 row): a test service that fails once then succeeds shows 'failed', then 'ok', in the
/// services view. The real host in a temp home, with a fake service added through <see cref="LoopbackBinding"/>'s
/// configure seam (plan Q1, option b); the scheduler and the projector are driven on the fake clock, and each step is
/// read both from the services view and through GET /sindri/services.
/// </summary>
public sealed class FlakyServiceTests : IDisposable
{
    private const string ProfileYaml = "schemaVersion: 1\nuser: flaky\nservices:\n  flaky: { every: 5s }\n";
    private static readonly ServiceName _flaky = ServiceName.From("flaky");
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FlakyService _service = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task A_test_service_that_fails_once_then_succeeds_shows_failed_then_ok()
    {
        await using var app = await Start();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
        var reader = app.Services.GetRequiredService<IViewReader>();
        var clock = new SteppedClock(_clock);
        using var scheduler = ActivatorUtilities.CreateInstance<SchedulerHost>(app.Services, clock);
        await scheduler.StartAsync(Token);

        await StepUntil(clock, runs: 1);
        var first = await Row(app, reader);
        var firstListed = await Listed(http);
        await StepUntil(clock, runs: 2);
        var second = await Row(app, reader);
        var secondListed = await Listed(http);
        await scheduler.StopAsync(Token);
        await app.StopAsync(Token);

        Assert.Equal((ServiceOutcome.Failed, FlakyService.Failure.Code), (first.LastRun!.Outcome, first.LastRun.Code));
        Assert.Equal(ServiceOutcome.Ok, second.LastRun!.Outcome);
        Assert.Null(second.LastRun.Code);
        Assert.Equal(first.LastRun.Slot.AddSeconds(5), second.LastRun.Slot);
        Assert.Equal((first.LastRun, first.LastRun.Slot.AddSeconds(5)), (firstListed.LastRun, firstListed.NextSlot));
        Assert.Equal((second.LastRun, second.LastRun.Slot.AddSeconds(5)), (secondListed.LastRun, secondListed.NextSlot));
    }

    private async Task<WebApplication> Start()
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), ProfileYaml, Token);
        var app = LoopbackBinding.Build([], n => n == "AW_SINDRI_HOME" ? _home.Path : null, _clock, services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IIdGenerator>(new SequentialIdGenerator()));
            _ = services.Replace(ServiceDescriptor.Singleton<IProcessProbe>(new FakeProcessProbe()));
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
        FirstPass.Publish(app.Services);
        await app.StartAsync(Token);
        return app;
    }

    /// <summary>Steps the clock a second at a time until the service has run <paramref name="runs"/> times.</summary>
    private async Task StepUntil(SteppedClock clock, int runs)
    {
        for (var step = 0; _service.Runs < runs; step++)
        {
            Assert.True(step < 60, "the service didn't run within 60 fake seconds");
            await clock.Step(TimeSpan.FromSeconds(1));
            await clock.Armed(() => _service.Runs >= runs);
        }

        await clock.Armed();   // the run's service.ran is appended before the loop arms its next wait
    }

    private static async Task<ServiceRow> Row(WebApplication app, IViewReader reader)
    {
        Assert.Equal(ProjectorPass.CaughtUp, await app.Services.GetRequiredService<Projector>().CatchUp(Token));
        return (await reader.Get<ServiceRow>(ViewNames.Services, _flaky.Value, Token)).Value!;
    }

    /// <summary>The flaky service as GET /sindri/services lists it (a query, so a session reads it).</summary>
    private static async Task<ServiceView> Listed(HttpClient http)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/sindri/services", UriKind.Relative)).AsSession();
        using var answer = await http.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var body = await answer.Content.ReadFromJsonAsync(HttpJson.Info<ListServicesResponse>(), Token);
        Assert.Null(body!.Empty);
        return Assert.Single(body.Services, s => s.Name == _flaky);
    }

    /// <summary>A service named 'flaky' whose first run returns an error and every later run succeeds.</summary>
    private sealed class FlakyService : IScheduledService
    {
        private int _runs;

        public static Error Failure { get; } = SindriErrors.ServiceFailed.With("flaky: the first run fails");

        public int Runs => Volatile.Read(ref _runs);

        public ServiceName Identity() => _flaky;

        public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ScheduledResult>>(Interlocked.Increment(ref _runs) == 1 ? Failure : ScheduledResult.Worked);
    }
}
