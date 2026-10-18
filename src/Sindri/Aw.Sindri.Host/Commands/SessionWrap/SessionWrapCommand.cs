using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// 'sindri session wrap --launch &lt;id&gt; -- &lt;agent cli …&gt;' (API spec §8.1, launcher spec §3, §4): runs the agent as a child, sets its Sindri environment, checks the
/// CLI is there (exit 127, SND-LAUNCH-003), sends a heartbeat every 30 s, waits up to 30 s for the agent's own session id and sends it on the next heartbeat,
/// and reports the exit. It exits with the agent's code.
/// </summary>
/// <param name="calls">The control plane calls.</param>
/// <param name="children">Starts the agent.</param>
/// <param name="environment">Wrap's environment.</param>
/// <param name="clock">The time the heartbeat and the id wait run on.</param>
/// <param name="errors">Where wrap writes its own messages (never the agent's output).</param>
/// <param name="ids">Where the session id is looked for.</param>
/// <param name="hangup">Completes when wrap is told to hang up or terminate (its terminal closed); null when nothing can tell it.</param>
internal sealed class SessionWrapCommand(IWrapCalls calls, IChildLauncher children, Func<string, string?> environment, TimeProvider clock, TextWriter errors, ISessionIdSource ids, Task? hangup = null)
{
    private static readonly TimeSpan _idWait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _idPoll = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _hangupGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _exitRetry = TimeSpan.FromSeconds(1);
    private const int ExitAttempts = 3;
    private SessionId? _found;
    private Func<string, string?>? _resolved;

    internal async Task<int> Run(string[] args, CancellationToken cancellationToken)
    {
        _resolved = WithSecretFromFile(environment);
        var (failed, begunLaunch, started) = await Begin(args).ConfigureAwait(false);
        if (failed is { } failure)
        {
            return failure;
        }

        using var child = started!;   // why: a start without a failure code has a child
        var launch = begunLaunch!.Value;   // why: set with the child
        var proof = Env("AW_SINDRI_LAUNCH_SECRET") is { Length: > 0 } value ? new LaunchSecret(value) : (LaunchSecret?)null;
        if (proof is null)
        {
            await errors.WriteLineAsync("AW_SINDRI_LAUNCH_SECRET is not set: this session sends no heartbeats").ConfigureAwait(false);
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watchers = new List<Task> { WaitForId(launch, clock.GetUtcNow(), stop.Token) };
        if (proof is { } secret)
        {
            watchers.Add(Beats(launch, secret, stop.Token));
        }

        var code = await ExitCode(child, cancellationToken).ConfigureAwait(false);
        await stop.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(watchers).ConfigureAwait(false);
        if (proof is { } last)
        {
            await Exit(launch, last, code, cancellationToken).ConfigureAwait(false);
        }

        return code;
    }

    private string? Env(string name) => (_resolved ?? environment)(name);

    /// <summary>
    /// The launcher never puts the launch secret in a command line: it names a 0600 file (AW_SINDRI_LAUNCH_SECRET_FILE). The file is read once and deleted, and the
    /// secret joins the environment the agent gets (and wrap's own proof). An AW_SINDRI_LAUNCH_SECRET already set wins; a missing or unreadable file means no secret.
    /// </summary>
    private static Func<string, string?> WithSecretFromFile(Func<string, string?> environment)
    {
        if (environment("AW_SINDRI_LAUNCH_SECRET") is { Length: > 0 } || environment("AW_SINDRI_LAUNCH_SECRET_FILE") is not { Length: > 0 } path)
        {
            return environment;
        }

        string? secret = null;
        try
        {
            secret = File.ReadAllText(path).Trim();
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // no file, no secret: wrap says so and sends no heartbeats
        }

        return name => string.Equals(name, "AW_SINDRI_LAUNCH_SECRET", StringComparison.Ordinal) ? secret : environment(name);
    }

    /// <summary>Waits for the agent. A hang-up (the terminal closed) is also sent to the agent by the kernel, but an agent that doesn't read its terminal can outlive it; after the grace it is killed, so the session ends and its exit is reported.</summary>
    private async Task<int> ExitCode(IWrapChild child, CancellationToken cancellationToken)
    {
        if (hangup is not null && await Task.WhenAny(child.Exited, hangup).ConfigureAwait(false) == hangup && !child.Exited.IsCompleted)
        {
            var grace = Task.Delay(_hangupGrace, clock, cancellationToken);
            if (await Task.WhenAny(child.Exited, grace).ConfigureAwait(false) == grace)
            {
                child.Dispose();   // kills the agent's tree; Exited completes with 137
            }
        }

        return await child.Exited.ConfigureAwait(false);
    }

    private async Task<(int? Failed, LaunchId? Launch, IWrapChild? Child)> Begin(string[] args)
    {
        var parsed = SessionWrapArguments.Parse(args);
        if (parsed.IsError)
        {
            await errors.WriteLineAsync($"{parsed.Error.Code}: {parsed.Error.Message}").ConfigureAwait(false);
            return (2, null, null);
        }

        var (launch, argv) = (parsed.Value.Launch, parsed.Value.Argv);
        if (!BinOnPath.Resolves(argv[0], Env("PATH")))
        {
            await errors.WriteLineAsync($"{argv[0]} not found ({SindriErrors.LaunchAgentMissing.Code}): install it, or run `sindri launch show` to see the catalog").ConfigureAwait(false);
            return (127, launch, null);
        }

        var started = children.Start(argv, WrapEnvironment.For(launch, Env));
        if (started.IsError)
        {
            await errors.WriteLineAsync($"{started.Error.Code}: {started.Error.Message}").ConfigureAwait(false);
            return (127, launch, null);
        }

        return (null, launch, started.Value);
    }

    private async Task Beats(LaunchId launch, LaunchSecret proof, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                _ = await calls.Heartbeat(launch, proof, _found, token).ConfigureAwait(false);   // a refused or unreachable heartbeat never stops the agent
                await Task.Delay(LaunchRules.HeartbeatEvery, clock, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // the agent exited
        }
    }

    private async Task WaitForId(LaunchId launch, DateTimeOffset start, CancellationToken token)
    {
        var ambiguous = false;
        try
        {
            while (clock.GetUtcNow() - start < _idWait)
            {
                var scan = await ids.Look(launch, start, clock.GetUtcNow() - start, token).ConfigureAwait(false);
                if (scan.Id is { } id)
                {
                    _found = id;
                    return;
                }

                ambiguous = scan.Ambiguous;
                await Task.Delay(_idPoll, clock, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ambiguous)
        {
            await errors.WriteLineAsync("ambiguous session id: more than one new session file matched, so no id is recorded for this launch").ConfigureAwait(false);
        }
    }

    private async Task Exit(LaunchId launch, LaunchSecret proof, int code, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= ExitAttempts; attempt++)
        {
            if ((await calls.Exit(launch, proof, code, _found, cancellationToken).ConfigureAwait(false)).IsSuccess || attempt == ExitAttempts)
            {
                return;   // after the last try the 120 s silence timer ends the session
            }

            await Task.Delay(_exitRetry, clock, cancellationToken).ConfigureAwait(false);
        }
    }
}
