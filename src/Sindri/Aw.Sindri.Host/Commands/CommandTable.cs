using System.Text.Json;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The verb table: 'serve' runs the control plane; every other verb runs as a client in a short-lived container built by
/// the same Compose, so the CLI and the API share one composition.
/// </summary>
internal static class CommandTable
{
    internal static async Task<int> Run(string[] args, Func<string, string?> environment, TimeProvider clock)
    {
        if (args is ["serve"])
        {
            return await ServeCommand.Run(environment, clock, Console.Error).ConfigureAwait(false);
        }

        var provider = Composition.Compose(new ServiceCollection(), environment, clock).BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            _ = provider.GetRequiredService<TelemetryRuntime>();
            var scope = provider.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                return await Dispatch(args, scope.ServiceProvider).ConfigureAwait(false);
            }
        }
    }

    private static async Task<int> Dispatch(string[] args, IServiceProvider services)
    {
        var options = services.GetRequiredService<JsonSerializerOptions>();
        var probe = services.GetRequiredService<IControlPlaneProbe>();
        var output = new Output(Console.Out, Console.Error, options);
        using var client = new ControlPlaneClient(probe, options);
        var verbs = new Verbs(client, probe, output, services);
        var ct = CancellationToken.None;
        return args switch
        {
            [] => await Start(services, client, output, headless: false).ConfigureAwait(false),
            ["--headless"] => await Start(services, client, output, headless: true).ConfigureAwait(false),
            ["status", .. var rest] when Json(rest) is { } json => await verbs.Status(json, ct).ConfigureAwait(false),
            ["stop"] => await verbs.Stop(ct).ConfigureAwait(false),
            ["profile", "validate", .. var rest] when Json(rest) is { } json => await verbs.Validate(json, ct).ConfigureAwait(false),
            ["profile", "approve"] => await verbs.Approve(hashText: null, Interactive(), Console.In, ct).ConfigureAwait(false),
            ["profile", "approve", var hash] => await verbs.Approve(hash, Interactive(), Console.In, ct).ConfigureAwait(false),
            ["doctor", .. var rest] when Json(rest) is { } json => await verbs.Doctor(json, ct).ConfigureAwait(false),
            ["workflow", "help"] => await output.Text(Usage.Workflow + "\n", 0).ConfigureAwait(false),
            ["workflow", .. var rest] => await new WorkflowVerbs(client, output, services).Run(rest, ct).ConfigureAwait(false),
            ["help" or "--help" or "-h"] => await output.Text(Usage.Text + "\n", 0).ConfigureAwait(false),
            _ => await UnknownVerb(args, output).ConfigureAwait(false),
        };
    }

    private static Task<int> Start(IServiceProvider services, ControlPlaneClient client, Output output, bool headless) =>
        new StartCommand(services.GetRequiredService<IControlPlaneProbe>(), services.GetRequiredService<IControlPlaneLauncher>(), client, output,
            services.GetRequiredService<TimeProvider>()).Run(headless, CancellationToken.None);

    private static bool? Json(string[] rest) => rest switch
    {
        [] => false,
        ["--json"] => true,
        _ => null,
    };

    private static bool Interactive() => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    private static async Task<int> UnknownVerb(string[] args, Output output)
    {
        _ = await output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a sindri command")).ConfigureAwait(false);
        await Console.Error.WriteLineAsync(Usage.Text).ConfigureAwait(false);
        return 2;
    }
}
