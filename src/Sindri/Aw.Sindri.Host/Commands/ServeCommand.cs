using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Host.Registration;
using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri serve': the control plane in the foreground (what bare 'sindri' starts detached, and what launchd will keep
/// alive). It chooses the port and checks its loopback listener is configured (nothing is bound yet), then takes the
/// lease, so a second serve is refused before it binds the port; then it listens, records the endpoint, and runs until a
/// stop request, SIGTERM/SIGINT or a lost lease. A failure to listen records the stop and releases the lease. SIGHUP is
/// ignored so closing the terminal doesn't stop it. Once the lease is taken (so a refused second serve never replaces the
/// running one's token) it mints the per-start human token, and the stop removes it beside control.json (only while it is still this start's token); serve drops it again on the way out for a stop that had no lease or whose removal failed. Every
/// in-process call runs as the caller 'service:host', the only place that caller is set (API spec §2.1).
/// </summary>
internal static class ServeCommand
{
    internal static async Task<int> Run(Func<string, string?> environment, TimeProvider clock, TextWriter error, Action<IServiceCollection>? configure = null)
    {
        using var hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, static c => c.Cancel = true);
        if (environment(LoopbackBinding.PortVariable) is { Length: > 0 } asked && !IsPort(asked))
        {
            await error.WriteLineAsync($"{SindriErrors.InvalidInput.Code} {LoopbackBinding.PortVariable}='{asked}' isn't a port number "
                + "(fix: set it to 0..65535, or unset it to use the profile's api.port)").ConfigureAwait(false);
            return 2;
        }

        var app = LoopbackBinding.Build([], environment, clock, services =>
        {
            _ = services.AddSindriServe();
            configure?.Invoke(services);   // tests only: Program passes nothing
        });
        await using (app.ConfigureAwait(false))
        {
            _ = Pipeline.Configure(app);
            var port = app.Services.GetRequiredService<ServePort>();
            _ = await port.Listen(environment(LoopbackBinding.PortVariable), CancellationToken.None).ConfigureAwait(false);
            if (port.Chosen is null)   // checked before the lease is taken; ServeListenerCheck is the backstop at start
            {
                await error.WriteLineAsync($"{SindriErrors.PortInUse.Code} serve chose no port, so it has no loopback listener and won't start "
                    + "(fix: report it; it is a bug)").ConfigureAwait(false);
                return 2;
            }

            var started = await Call<StartProcessRequest, StartProcessResponse>(app, new StartProcessRequest()).ConfigureAwait(false);
            if (started.IsError)
            {
                await error.WriteLineAsync($"{started.Error.Code} {started.Error.Message}").ConfigureAwait(false);
                return started.Error.Code == SindriErrors.LeaseHeld.Code ? 1 : 2;
            }

            if (!await MintToken(app, error).ConfigureAwait(false) || !await Listen(app, error).ConfigureAwait(false))
            {
                return 2;
            }

            return await RunUntilStopped(app, started.Value).ConfigureAwait(false);
        }
    }

    /// <summary>Mints this start's human token and holds it; on failure records the stop and releases the lease.</summary>
    private static async Task<bool> MintToken(WebApplication app, TextWriter error)
    {
        var minted = await app.Services.GetRequiredService<IHumanTokenStore>().Mint(CancellationToken.None).ConfigureAwait(false);
        if (minted.IsError)
        {
            _ = await Call<StopProcessRequest, StopProcessResponse>(app, new StopProcessRequest(StopReason.ListenFailed)).ConfigureAwait(false);
            await error.WriteLineAsync($"{minted.Error.Code} serve couldn't write the human token: {minted.Error.Message}").ConfigureAwait(false);
            return false;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold(minted.Value);
        return true;
    }

    private static async Task<bool> Listen(WebApplication app, TextWriter error)
    {
        try
        {
            await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is IOException or SocketException)   // taken, privileged or refused
        {
            _ = await Call<StopProcessRequest, StopProcessResponse>(app, new StopProcessRequest(StopReason.PortInUse)).ConfigureAwait(false);
            await error.WriteLineAsync($"{SindriErrors.PortInUse.Code} {e.Message} (fix: free the port, or set AW_SINDRI_PORT)").ConfigureAwait(false);
            return false;
        }
        catch (Exception e)   // a hosted service refused to start (ServeListenerCheck among them): never leave the lease held
        {
            _ = await Call<StopProcessRequest, StopProcessResponse>(app, new StopProcessRequest(StopReason.ListenFailed)).ConfigureAwait(false);
            await error.WriteLineAsync($"{SindriErrors.PortInUse.Code} serve couldn't start listening: {e.Message} (fix: see the log above; run sindri doctor)").ConfigureAwait(false);
            return false;
        }
    }

    private static async Task<int> RunUntilStopped(WebApplication app, StartProcessResponse started)
    {
        var grant = started.Grant;
        var shutdown = app.Services.GetRequiredService<ProcessShutdown>();
        using var either = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Stopping, app.Lifetime.ApplicationStopping);

        // the Temporal worker polls the dev server until serve stops, and stops before the lease goes
        var worker = started.Temporal is { } temporal
            ? app.Services.GetRequiredService<ITemporalWorker>().Run(temporal, either.Token)
            : Task.CompletedTask;
        var runtime = app.Services.GetRequiredService<TemporalRuntime>();
        runtime.Track(worker);   // StopProcess waits for it before it stops the dev server

        // written once the worker is tracked: a client that finds control.json can stop serve, and that stop waits for it
        var endpoints = app.Services.GetRequiredService<IEndpointFile>();
        var url = new Uri(app.Urls.First());
        var endpoint = new ControlEndpoint(url, grant.Holder.Pid, grant.Epoch) { Temporal = started.Temporal };   // the dev server's pid and ports
        _ = await endpoints.Write(endpoint, CancellationToken.None).ConfigureAwait(false);
        var onyx = OnyxServeHooks.Start(app, endpoint, endpoints, either.Token);   // a managed Onyx comes up in the background; an unmanaged one is left alone
        try
        {
            await Task.Delay(Timeout.Infinite, either.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // a stop request, a signal or a lost lease
        }

        await StopLaunchedAndOnyx(app, onyx).ConfigureAwait(false);   // launched sessions end (each recorded) and a managed Onyx stops, before the record and the lease go
        await runtime.WaitForWorker(CancellationToken.None).ConfigureAwait(false);   // bounded, and a worker fault is the worker's to log

        if (app.Services.GetRequiredService<HeldLease>().Current is not null)
        {
            _ = await Call<StopProcessRequest, StopProcessResponse>(app, new StopProcessRequest(StopReason.Signal)).ConfigureAwait(false);
        }

        // StopProcess removes the record before releasing the lease; this catches the rest: a lost lease, a stop with no grant, or a stop whose removal failed
        _ = await endpoints.Remove(grant.Holder.Pid, CancellationToken.None).ConfigureAwait(false);
        await DropToken(app).ConfigureAwait(false);
        await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        return app.Services.GetRequiredService<IExitCode>().Code;
    }

    private static async Task StopLaunchedAndOnyx(WebApplication app, Task onyx)
    {
        await EndLaunchedSessions(app).ConfigureAwait(false);
        await OnyxServeHooks.Stop(app, onyx, Console.Error).ConfigureAwait(false);
    }

    /// <summary>Ends the sessions this serve launched on the sindri-launch tmux socket, so none outlives it (bounded; a failure is logged, never fatal).</summary>
    private static async Task EndLaunchedSessions(WebApplication app)
    {
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            using (app.Services.GetRequiredService<ICallerContextAccessor>().EnterService("host"))
            {
                var ended = await app.Services.GetRequiredService<ILaunchShutdown>().StopAll(bound.Token).ConfigureAwait(false);
                if (ended > 0)
                {
                    await Console.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"sindri: ended {ended} launched session(s) on stop")).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("sindri: ending the launched sessions overran 20 s; the heartbeat sweep marks the rest lost").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Forgets this start's token and removes its file if it still holds this start's token (like control.json: a
    /// serve that lost its lease leaves its successor's token alone).
    /// </summary>
    private static async Task DropToken(WebApplication app)
    {
        var held = app.Services.GetRequiredService<HeldHumanToken>();
        var mine = held.Current;
        held.Clear();
        if (mine is not null)
        {
            _ = await app.Services.GetRequiredService<IHumanTokenStore>().Remove(mine, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one service in its own scope as the caller 'service:host'.</summary>
    private static async Task<Kernel.Result<TRes>> Call<TReq, TRes>(WebApplication app, TReq request)
        where TReq : notnull
    {
        using (app.Services.GetRequiredService<ICallerContextAccessor>().EnterService("host"))
        {
            var scope = app.Services.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                return await scope.ServiceProvider.GetRequiredService<IService<TReq, TRes>>().Handle(request, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static bool IsPort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port <= 65535;
}
