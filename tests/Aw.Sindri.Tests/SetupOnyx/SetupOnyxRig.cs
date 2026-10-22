using System.Diagnostics;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ConnectOnyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Features.SetupOnyx;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands.Authorization;
using Aw.Sindri.Tests.Jobs;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.SetupOnyx;

/// <summary>The Onyx setup job and the login command on the real job runtime over in-memory ports: Onyx's `up` is a stub, everything after the owner's login is the real service over Onyx fakes.</summary>
internal sealed class SetupOnyxRig : IAsyncDisposable
{
    /// <summary>The text every test sends as the owner's login secret; nothing may print it.</summary>
    internal const string Sentinel = "sentinel-owner-value-9f3c1d";

    internal const string OwnerEmail = "owner@example.com";

    public SetupOnyxRig()
    {
        Runtime = new RealJobRuntime(
            rows: [Rows.Of(ActionClass.Job, "SetupOnyx", typeof(SetupOnyxRequest)), Rows.Of(ActionClass.Desk, "ConnectOnyx", typeof(ConnectOnyxRequest))],
            configure: (services, h) => services
                    .AddSingleton<IOnyxWorkspace>(Workspace).AddSingleton<IOnyxPrompts>(Prompts).AddSingleton<IBrowser>(Browser).AddSingleton<IOnyxProvisioning>(Provisioning)
                    .AddSingleton<ISecretWriter>(Secrets).AddSingleton<ISecretPointers>(Secrets).AddSingleton<IOnyxProfileWriter>(Profile)
                    .AddSingleton<IOnyxProgress>(Progress).AddSingleton<IRandomHex>(new FakeRandomHex()).AddSingleton<ILedgerReader>(h.Store)
                    .AddScoped<OnyxKeyMinter>().AddScoped<OnyxAccountService>()
                    .AddScoped<IService<OnyxUpRequest, OnyxUpResponse>>(_ => Up)
                    .AddOnyxSetup());
    }

    internal RealJobRuntime Runtime { get; }

    internal FakeOnyxWorkspace Workspace { get; } = new();

    internal FakeOnyxPrompts Prompts { get; } = new();

    internal FakeBrowser Browser { get; } = new();

    internal FakeOnyxProvisioning Provisioning { get; } = new();

    internal FakeSecretWriter Secrets { get; } = new();

    internal FakeOnyxProfileWriter Profile { get; } = new();

    internal CapturingOnyxProgress Progress { get; } = new();

    internal StubOnyxUp Up { get; } = new();

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal static CallerContext Human() => A.Caller().Human().Build();

    internal IJobSignals Signals => Runtime.Get<IJobSignals>();

    internal IOnyxLoginSlots Slots => Runtime.Get<IOnyxLoginSlots>();

    /// <summary>Opens the runtime's lease and starts the setup job as the human.</summary>
    /// <returns>The job.</returns>
    internal async Task<JobId> Start()
    {
        await Runtime.Open();
        var started = await Runtime.Start(new SetupOnyxRequest(), Human());
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        return started.Value.JobId;
    }

    /// <summary>Waits (a hang guard on the real clock) until <paramref name="job"/> is parked on the login signal.</summary>
    /// <param name="job">The job.</param>
    /// <returns>A task that completes then.</returns>
    internal async Task Waiting(JobId job)
    {
        var started = Stopwatch.GetTimestamp();
        while (!Signals.IsWaiting(job, OnyxSetupJob.Signal))
        {
            Assert.True(Stopwatch.GetElapsedTime(started) < JobHost.Hang, "the setup job never parked on the login signal");
            await Task.Delay(10, Token);
        }
    }

    /// <summary>Sends the owner's login as the human: through the request's hand-over, as the endpoint does.</summary>
    /// <param name="job">The job to wake.</param>
    /// <param name="email">The email.</param>
    /// <param name="secret">The secret text.</param>
    /// <returns>The answer.</returns>
    internal Task<Result<JobStartedResponse>> Connect(JobId job, string email = OwnerEmail, string secret = Sentinel) =>
        Runtime.RunPrepared<ConnectOnyxRequest, JobStartedResponse>(new ConnectOnyxRequest(job), Human(),
            services => services.GetRequiredService<IOnyxLoginHand>().Give(new OnyxLogin(email, new OnyxSecret(secret))));

    /// <summary>Everything the setup could have written down or shown: each ledger event (type, key and payload), the progress lines, Onyx's state and the audit log.</summary>
    /// <returns>One text to search.</returns>
    internal string Everything()
    {
        var logged = Runtime.Get<ILogger<AuditTrail>>() is ListLogger<AuditTrail> log ? log.Lines : [];
        return string.Join('\n', Runtime.Events.SelectMany(static e => new[] { e.Envelope.Type, e.IdempotencyKey, e.Envelope.Payload }))
            + "\n" + string.Join('\n', Progress.Lines)
            + "\n" + string.Join('\n', Workspace.State?.Steps.Values ?? []) + Workspace.State?.KeyPointer
            + "\n" + string.Join('\n', logged);
    }

    public ValueTask DisposeAsync() => Runtime.DisposeAsync();

    /// <summary>Onyx's `up`, stubbed: it answers a running Onyx on 3000, or the error a test sets.</summary>
    internal sealed class StubOnyxUp : IService<OnyxUpRequest, OnyxUpResponse>
    {
        public Error? Fails { get; set; }

        public ValueTask<Result<OnyxUpResponse>> Handle(OnyxUpRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<OnyxUpResponse>>(Fails is { } error ? error : new OnyxUpResponse(3000, "http://127.0.0.1:3000", "v4.9.0", [], []));
    }
}
