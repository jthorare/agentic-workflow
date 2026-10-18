using System.Text.Json;
using Aw.Context.Core.Facade;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

using Aw.Sindri.Host.Commands.Probe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The verb table: 'serve' runs the control plane; every other verb runs as a client in a short-lived container built by
/// the same Compose, so the CLI and the API share one composition. The CLI classes itself first (API spec §6): that sets
/// its caller headers and whether it may start the control plane (§7).
/// </summary>
internal static class CommandTable
{
    internal static async Task<int> Run(string[] args, Func<string, string?> environment, TimeProvider clock)
    {
        if (args is ["serve"])
        {
            return await Serve(environment, clock).ConfigureAwait(false);
        }

        var provider = Composition.Compose(new ServiceCollection(), environment, clock).BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            _ = provider.GetRequiredService<TelemetryRuntime>();
            var scope = provider.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var session = AgentSession.Detect(environment, scope.ServiceProvider.GetRequiredService<IProcessAncestry>(), Interactive());
                return await Dispatch(args, session, scope.ServiceProvider, environment).ConfigureAwait(false);
            }
        }
    }

    /// <summary>'sindri serve', refused inside an agent session unless AW_SINDRI_PROVER=1 (P1 plan G6).</summary>
    private static async Task<int> Serve(Func<string, string?> environment, TimeProvider clock)
    {
        Kernel.Error? refused;
        Output output;
        var provider = Composition.Compose(new ServiceCollection(), environment, clock).BuildServiceProvider();
        await using (provider.ConfigureAwait(false))   // only to class the CLI: serve builds its own host
        {
            refused = StartCommand.ServeRefusal(AgentSession.Detect(environment, provider.GetRequiredService<IProcessAncestry>(), Interactive()));
            output = new Output(Console.Out, Console.Error, provider.GetRequiredService<JsonSerializerOptions>());
        }

        return refused is null
            ? await ServeCommand.Run(environment, clock, Console.Error).ConfigureAwait(false)
            : await output.Error(refused).ConfigureAwait(false);
    }

    private static async Task<int> Dispatch(string[] args, AgentSession session, IServiceProvider services, Func<string, string?> environment)
    {
        var options = services.GetRequiredService<JsonSerializerOptions>();
        var probe = services.GetRequiredService<IControlPlaneProbe>();
        var output = new Output(Console.Out, Console.Error, options);
        using var client = new ControlPlaneClient(probe, options, Headers(session, services));
        StartCommand Start(bool headless) => new(probe, services.GetRequiredService<IControlPlaneLauncher>(), client, output,
            services.GetRequiredService<TimeProvider>(), session, UiOpener(headless, environment));
        var start = Start(headless: true);   // 'sindri --headless', and the start a human 'profile approve' may need
        var verbs = new Verbs(client, probe, output, services);
        using var interrupt = Interrupt.For(args);   // Ctrl-C ends 'workflow events --follow' with exit 0; no other verb listens
        var ct = interrupt.Token;
        return args switch
        {
            [] => await Start(headless: false).Run(ct).ConfigureAwait(false),
            ["--headless"] => await start.Run(ct).ConfigureAwait(false),
            ["status", .. var rest] when Follow(rest) is { } json => await new FollowVerbs(client, probe, output, services.GetRequiredService<TimeProvider>()).Run(json, ct).ConfigureAwait(false),
            ["status", .. var rest] when Json(rest) is { } json => await verbs.Status(json, ct).ConfigureAwait(false),
            ["stop"] => await verbs.Stop(ct).ConfigureAwait(false),
            ["profile", "validate", .. var rest] when Json(rest) is { } json => await verbs.Validate(json, ct).ConfigureAwait(false),
            ["profile", "approve", .. var rest] when Approval(rest) is { } a => await new Verbs(client, probe, output, services, c => start.EnsureRunning(!a.NoStart, c))
                .Approve(a.Hash, Interactive(), Console.In, ct).ConfigureAwait(false),
            ["views", "rebuild", .. var rest] when NoStart(rest) is { } noStart => await new Verbs(client, probe, output, services, c => start.EnsureRunning(!noStart, c))
                .RebuildViews(ct).ConfigureAwait(false),
            ["open", .. var rest] => await new OpenCommand(client, output, start.EnsureRunning, services.GetRequiredService<TimeProvider>()).Run(rest, ct).ConfigureAwait(false),
            ["queue" or "items" or "item", ..] => await new ItemVerbs(client, output, (launch, c) => start.EnsureRunning(launch, c)).Run(args, ct).ConfigureAwait(false),
            ["request" or "requests" or "decisions" or "decide" or "needs-you", ..] => await new RequestVerbs(client, output, (launch, c) => start.EnsureRunning(launch, c)).Run(args, ct).ConfigureAwait(false),
            ["agents", .. var rest] => await new AgentsVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
            ["ledger", .. var rest] => await new LedgerVerbs(client, output, (launch, c) => start.EnsureRunning(launch, c)).Run(rest, ct).ConfigureAwait(false),
            ["services" or "jobs", ..] => await JobCommands.Run(args, (client, output), start.EnsureRunning, services.GetRequiredService<TimeProvider>(), (ct, interrupt.Again)).ConfigureAwait(false),
            ["launch" or "session", ..] => await LaunchOrSession(args, client, (output, environment), services, (ct, interrupt.Again)).ConfigureAwait(false),
            ["doctor", ..] => await Doctor(args, verbs, Harnesses(services, environment), output, ct).ConfigureAwait(false),
            ["mcp", .. var rest] => await new McpVerbs(services, output, session).Run(rest, ct).ConfigureAwait(false),
            ["context", ..] => await Context(args, client, output, services, (ct, interrupt.Again)).ConfigureAwait(false),
            ["recipe" or "workflow" or "outbox" or "handback", ..] => await Workflows(args, services, output, new RecipeDesk(client, Console.In, c => start.EnsureRunning(launch: true, c), environment), ct).ConfigureAwait(false),
            ["help" or "--help" or "-h"] => await output.Text(Usage.Text + "\n", 0).ConfigureAwait(false),
            _ => await UnknownVerb(args, output).ConfigureAwait(false),
        };
    }

    /// <summary>'recipe', 'workflow', 'outbox' and the short 'handback'.</summary>
    private static async Task<int> Workflows(string[] args, IServiceProvider services, Output output, RecipeDesk desk, CancellationToken ct) =>
        args switch
        {
            ["recipe", .. var rest] => await RecipeCommandTable.Run(rest, services, output, desk, ct).ConfigureAwait(false),
            ["workflow", .. var rest] => await WorkflowCommandTable.Run(rest, services, output, desk, ct).ConfigureAwait(false),
            ["outbox", .. var rest] => await OutboxCommandTable.Run(rest, services, output, ct).ConfigureAwait(false),
            ["handback", ..] => await WorkflowCommandTable.RunShort(args, services, output, desk, ct).ConfigureAwait(false),
            _ => await UnknownVerb(args, output).ConfigureAwait(false),
        };

    /// <summary>'launch init|show' (box-local) and 'session wrap' (runs the agent under a launch).</summary>
    private static async Task<int> LaunchOrSession(string[] args, ControlPlaneClient client, (Output Output, Func<string, string?> Environment) io, IServiceProvider services, (CancellationToken Interrupt, CancellationToken Again) signals) =>
        args switch
        {
            ["session", "wrap", .. var rest] => await SessionWrapVerb.Run(rest, client, services.GetRequiredService<ISindriHome>(), io.Environment, signals.Interrupt).ConfigureAwait(false),
            ["launch", "probe", .. var probe] => await new LaunchProbeVerb(client, io.Output, new JobFollower(client, io.Output, services.GetRequiredService<TimeProvider>())).Run(probe, signals.Interrupt, signals.Again).ConfigureAwait(false),
            ["launch", .. var rest] => await new LaunchVerbs(services.GetRequiredService<ILaunchSetup>(), io.Output).Run(rest, signals.Interrupt).ConfigureAwait(false),
            _ => await UnknownVerb(args, io.Output).ConfigureAwait(false),
        };

    private static ProbeCommands Harnesses(IServiceProvider services, Func<string, string?> environment) =>
        new(services.GetRequiredService<IProcessAncestry>(), services.GetRequiredService<IHarnessVersions>(), environment, Interactive());

    /// <summary>'sindri doctor [--json]' and 'sindri doctor --probe [--harness NAME] [--json]'.</summary>
    private static async Task<int> Doctor(string[] args, Verbs verbs, ProbeCommands harnesses, Output output, CancellationToken ct) =>
        args switch
        {
            ["doctor", "--probe"] or ["doctor", "--probe", "--json"] => await output.Json(await harnesses.Probe(harness: null, ct).ConfigureAwait(false), 0).ConfigureAwait(false),
            ["doctor", "--probe", "--harness", var name, .. var rest] when Json(rest) is not null => await output.Json(await harnesses.Probe(name, ct).ConfigureAwait(false), 0).ConfigureAwait(false),
            ["doctor", .. var rest] when Json(rest) is { } json => await verbs.Doctor(json, ct, harnesses.DoctorChecks).ConfigureAwait(false),
            _ => await UnknownVerb(args, output).ConfigureAwait(false),
        };

    /// <summary>'sindri context …': one arm per verb family; the first that matches wins.</summary>
    private static async Task<int> Context(string[] args, ControlPlaneClient client, Output output, IServiceProvider services, (CancellationToken Interrupt, CancellationToken Again) signals) =>
        args switch
        {
            ["context", "sources", "sync", .. var rest] => await new ContextJobVerbs(client, output, new JobFollower(client, output, services.GetRequiredService<TimeProvider>())).Run(["sources", "sync", .. rest], signals.Interrupt, signals.Again).ConfigureAwait(false),
            ["context", "map", "build", .. var rest] => await new ContextJobVerbs(client, output, new JobFollower(client, output, services.GetRequiredService<TimeProvider>())).Run(["map", "build", .. rest], signals.Interrupt, signals.Again).ConfigureAwait(false),
            ["context", "show" or "neighbors" or "path", ..] => await new ContextGraphVerbs(client, output).Run(args[1..], signals.Interrupt).ConfigureAwait(false),
            ["context", "eval", "backtest", .. var rest] => await new ContextJobVerbs(client, output, new JobFollower(client, output, services.GetRequiredService<TimeProvider>())).Run(["eval", "backtest", .. rest], signals.Interrupt, signals.Again).ConfigureAwait(false),
            ["context", "eval", .. var rest] => await new ContextEvalVerbs(client, output, services.GetRequiredService<IContextEngine>()).Run(rest, signals.Interrupt).ConfigureAwait(false),
            ["context", "filemap", "accept" or "reject", var link] => await new ContextMapVerbs(client, output, Interactive(), Console.In).Run(["review", args[2], link], signals.Interrupt).ConfigureAwait(false),
            ["context", "lenses" or "filemap", ..] => await new ContextLensVerbs(client, output).Run(args[1..], signals.Interrupt).ConfigureAwait(false),
            ["context", "map", .. var rest] => await new ContextMapVerbs(client, output, Interactive(), Console.In).Run(rest, signals.Interrupt).ConfigureAwait(false),
            ["context", .. var rest] => await new ContextVerbs(client, output).Run(rest, signals.Interrupt).ConfigureAwait(false),
            _ => await UnknownVerb(args, output).ConfigureAwait(false),
        };

    /// <summary>
    /// The caller headers. The token store is Infrastructure's (registered with it); a missing token file means a human
    /// call carries no token and the API refuses it (SND-AUTH-001). The repo is looked up at the first call, from the
    /// working directory.
    /// </summary>
    private static CallerHeaders Headers(AgentSession session, IServiceProvider services) =>
        CallerHeaders.For(session, services.GetRequiredService<IHumanTokenStore>(),
            ct => CallerHeaders.RepoOf(services.GetRequiredService<IApprovals>(), services.GetRequiredService<IRepoLocator>(), Environment.CurrentDirectory, ct));

    /// <summary>What bare 'sindri' does to open the app; 'sindri --headless' opens nothing.</summary>
    internal static Func<string>? UiOpener(bool headless, Func<string, string?> environment) =>
        headless ? null : () => UiLauncher.Open(UiLauncher.Locate(environment, AppContext.BaseDirectory));

    /// <summary>'status --follow [--json]': whether it asks for JSON, or null when it isn't a follow.</summary>
    private static bool? Follow(string[] rest) => rest switch
    {
        ["--follow"] => false,
        ["--follow", "--json"] or ["--json", "--follow"] => true,
        _ => null,
    };

    private static bool? Json(string[] rest) => rest switch
    {
        [] => false,
        ["--json"] => true,
        _ => null,
    };

    /// <summary>'views rebuild [--no-start]'.</summary>
    private static bool? NoStart(string[] rest) => rest switch
    {
        [] => false,
        ["--no-start"] => true,
        _ => null,
    };

    /// <summary>'profile approve [--no-start] [&lt;hash&gt;]', the flag on either side of the hash.</summary>
    private static (string? Hash, bool NoStart)? Approval(string[] rest) => rest switch
    {
        [] => (null, false),
        ["--no-start"] => (null, true),
        ["--no-start", var hash] => (hash, true),
        [var hash, "--no-start"] => (hash, true),
        [var hash] when !hash.StartsWith("--", StringComparison.Ordinal) => (hash, false),
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
