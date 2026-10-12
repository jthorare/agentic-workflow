using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
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
/// ignored so closing the terminal doesn't stop it.
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

            if (!await Listen(app, error).ConfigureAwait(false))
            {
                return 2;
            }

            return await RunUntilStopped(app, started.Value.Grant).ConfigureAwait(false);
        }
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

    private static async Task<int> RunUntilStopped(WebApplication app, LeaseGrant grant)
    {
        var endpoints = app.Services.GetRequiredService<IEndpointFile>();
        var url = new Uri(app.Urls.First());
        _ = await endpoints.Write(new ControlEndpoint(url, grant.Holder.Pid, grant.Epoch), CancellationToken.None).ConfigureAwait(false);
        var shutdown = app.Services.GetRequiredService<ProcessShutdown>();
        using var either = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Stopping, app.Lifetime.ApplicationStopping);
        try
        {
            await Task.Delay(Timeout.Infinite, either.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // a stop request, a signal or a lost lease
        }

        if (app.Services.GetRequiredService<HeldLease>().Current is not null)
        {
            _ = await Call<StopProcessRequest, StopProcessResponse>(app, new StopProcessRequest(StopReason.Signal)).ConfigureAwait(false);
        }

        _ = await endpoints.Remove(grant.Holder.Pid, CancellationToken.None).ConfigureAwait(false);
        await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        return app.Services.GetRequiredService<IExitCode>().Code;
    }

    private static async Task<Kernel.Result<TRes>> Call<TReq, TRes>(WebApplication app, TReq request)
        where TReq : notnull
    {
        var scope = app.Services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredService<IService<TReq, TRes>>().Handle(request, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static bool IsPort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port <= 65535;
}
