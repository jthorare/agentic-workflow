using System.Net.Sockets;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The outside half of §3.2: the socket directory, the Sindri forward and the proxy, for one launch.</summary>
/// <param name="resolver">Resolves names for the proxy.</param>
/// <param name="dialer">Opens outbound connections.</param>
/// <param name="environment">Reads an environment variable (AW_SANDBOX_SOCAT, PATH).</param>
/// <param name="runDirectory">Where egress.log goes.</param>
/// <param name="warn">Receives a warning line (a failed flush); null writes a JSON line to stderr.</param>
internal sealed class EgressService(IEgressResolver resolver, IEgressDialer dialer, Func<string, string?> environment, string runDirectory, Action<string>? warn = null)
{
    /// <summary>Starts the listeners.</summary>
    /// <param name="network">The plan's network (its NetDir is created when missing).</param>
    /// <param name="caps">The caps.</param>
    /// <param name="denials">The launch's denial counter; the proxy counts into it and the flush loop writes and publishes it. Null counts nothing.</param>
    /// <param name="time">The clock the proxy checks the learn expiry against; null is the system clock.</param>
    /// <returns>The running egress, or -014 no-forwarder, or -016 net-forward when the socket paths don't fit.</returns>
    public Result<RunningEgress> Start(PlanNetwork network, EgressCaps caps, DenialCounter? denials = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(caps);
        if (FindSocat() is not { } socat)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoForwarder, "socat was not found");
        }

        if (!EgressSocketDirectory.SocketPathsFit(network.NetDir))
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the socket directory path is too long");
        }

        EgressListener? proxyListener = null;
        EgressListener? sindriListener = null;
        var started = false;
        try
        {
            EgressSocketDirectory.Create(network.NetDir);
            _ = Directory.CreateDirectory(runDirectory);
            var (log, proxy) = Prepare(network, caps, denials, time);
            var forward = new EgressForward(network.SindriPort);
#pragma warning disable CA2000 // ownership passes to RunningEgress, which disposes both
            proxyListener = EgressListener.Start(Path.Combine(network.NetDir, EgressSocketDirectory.ProxySocket), proxy.Handle);
            sindriListener = EgressListener.Start(Path.Combine(network.NetDir, EgressSocketDirectory.SindriSocket), forward.Handle);
#pragma warning restore CA2000
            started = true;
            return new RunningEgress(socat, network.NetDir, log, proxyListener, sindriListener, warn ?? WriteWarning) { Denials = denials };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SocketException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the forwarders could not start: " + e.Message);
        }
        finally
        {
            if (!started)
            {
                Undo(network.NetDir, proxyListener, sindriListener);
            }
        }
    }

    /// <summary>Starts only the proxy, on a free loopback TCP port (macOS, SB-9b): there is no network namespace to bridge into and the agent reaches Sindri's own port directly, so no socat, no socket directory and no Sindri forwarder. Same proxy, log and denial counting as <see cref="Start"/>.</summary>
    /// <param name="network">The plan's network; only its mode, groups and learn expiry are used.</param>
    /// <param name="caps">The caps.</param>
    /// <param name="denials">The launch's denial counter; null counts nothing.</param>
    /// <param name="time">The clock the proxy checks the learn expiry against; null is the system clock.</param>
    /// <returns>The running egress, whose <see cref="RunningEgress.ProxyPort"/> is the bound port, or -016 net-forward when the listener could not start.</returns>
    public Result<RunningEgress> StartLoopback(PlanNetwork network, EgressCaps caps, DenialCounter? denials = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(caps);
        EgressListener? listener = null;
        try
        {
            _ = Directory.CreateDirectory(runDirectory);
            var (log, proxy) = Prepare(network, caps, denials, time);
#pragma warning disable CA2000 // ownership passes to RunningEgress, which disposes it
            listener = EgressListener.StartLoopback(proxy.Handle);
#pragma warning restore CA2000
            return new RunningEgress(string.Empty, string.Empty, log, listener, sindri: null, warn ?? WriteWarning) { Denials = denials };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SocketException)
        {
            listener?.Abort();
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the proxy listener could not start: " + e.Message);
        }
    }

    private static void Undo(string directory, params EgressListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            listener?.Abort();
        }

        RemoveDirectory(directory);
    }

    private (EgressLog Log, EgressProxy Proxy) Prepare(PlanNetwork network, EgressCaps caps, DenialCounter? denials, TimeProvider? time)
    {
        var log = new EgressLog(Path.Combine(runDirectory, "egress.log"));
        log.Flush();
        return (log, new EgressProxy(new EgressPolicy(network.Groups, resolver, network.Mode, network.LearnUntil, time), dialer, log, caps) { Denials = denials });
    }

    private static void WriteWarning(string message) =>
        Console.Error.WriteLine("{\"level\":\"warn\",\"source\":\"sindri.egress\",\"message\":\"" + System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(message) + "\"}");

    private static void RemoveDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing more to undo; the launch is refused either way.
        }
    }

    private string? FindSocat()
    {
        if (environment(SandboxEnvironment.Socat) is { Length: > 0 } configured)
        {
            return File.Exists(configured) ? configured : null;
        }

        return (environment("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Path.Combine(d, "socat")).FirstOrDefault(File.Exists);
    }
}
#pragma warning restore MA0182
