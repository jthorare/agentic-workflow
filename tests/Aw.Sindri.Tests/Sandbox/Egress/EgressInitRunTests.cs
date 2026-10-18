using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>Runs the rendered init under sh, with a fake socat that records what it sees, and with the real socat.</summary>
[Collection(EgressProcesses.Name)]
public sealed class EgressInitRunTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Write(string name, string text)
    {
        var path = Path.Combine(_temp.Path, name);
        File.WriteAllText(path, text);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    private string FakeSocat(string body) => Write("socat", "#!/bin/sh\n" + body);

    private string Init(string socat, string inside = EgressInit.InsideDirectory) =>
        Write("init.sh", EgressInit.Render(new PlanNetwork(NetworkMode.Enforce, [], 7480, 3128, "/unused"), socat, Path.Combine(_temp.Path, "socat.log"), inside));

    private Process Run(string init, params string[] agent) => RunWith("/bin/sh", init, agent);

    private Process RunWith(string shell, string init, params string[] agent)
    {
        // The outer sh opens fd 5 on a file, then becomes the init: the init must close it before anything else runs.
        var outer = $"exec 5>'{Path.Combine(_temp.Path, "fd5")}'; exec {shell} '{init}' \"$@\"";
        var info = new ProcessStartInfo("/bin/sh", ["-c", outer, "sh", .. agent]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        return Process.Start(info)!;
    }

    private static bool Alive(int pid)
    {
        var stat = "/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat";
        return File.Exists(stat) && !File.ReadAllText(stat).Contains(") Z", StringComparison.Ordinal);
    }

    private async Task<int> PidFile(string name)
    {
        var path = Path.Combine(_temp.Path, name);
        for (var i = 0; i < 100; i++)
        {
            if (File.Exists(path) && int.TryParse((await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).Trim(), CultureInfo.InvariantCulture, out var pid))
            {
                return pid;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException(name);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Init_closes_inherited_fds_exports_NO_PROXY_returns_the_agents_status_and_stops_both_socats()
    {
        Assert.SkipWhen(!Directory.Exists("/proc/self/fd"), "needs /proc");
        var socat = FakeSocat($"echo $$ >> '{_temp.Path}/socat-pids'; [ -e /proc/$$/fd/5 ] && echo open >> '{_temp.Path}/socat-fd5'; exec sleep 30\n");
        var agent = Write("agent", $"#!/bin/sh\n[ -e /proc/$$/fd/5 ] && echo open > '{_temp.Path}/agent-fd5'\necho \"$NO_PROXY\" > '{_temp.Path}/agent-noproxy'\nexit 7\n");

        using var init = Run(Init(socat), agent);
        await init.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(7, init.ExitCode);
        Assert.False(File.Exists(Path.Combine(_temp.Path, "socat-fd5")));
        Assert.False(File.Exists(Path.Combine(_temp.Path, "agent-fd5")));
        Assert.Equal("127.0.0.1,localhost,::1", (await File.ReadAllTextAsync(Path.Combine(_temp.Path, "agent-noproxy"), TestContext.Current.CancellationToken)).Trim());
        var pids = (await File.ReadAllLinesAsync(Path.Combine(_temp.Path, "socat-pids"), TestContext.Current.CancellationToken)).Select(l => int.Parse(l, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(2, pids.Length);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(pids, Alive);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("TERM")]
    [InlineData("HUP")]
    [InlineData("INT")]
    public async Task A_signal_to_the_init_reaches_the_agent_and_the_socats_go_with_it(string signal)
    {
        Assert.SkipWhen(!Directory.Exists("/proc/self/fd"), "needs /proc");
        Assert.SkipWhen(signal == "INT" && SignalIgnoredHere(2), "the test host started with SIGINT ignored, which a shell cannot trap");
        var socat = FakeSocat($"echo $$ >> '{_temp.Path}/socat-pids'; exec sleep 30\n");
        var agent = Write("agent", $"#!/bin/sh\ntrap 'echo got-term > \"{_temp.Path}/agent-term\"; exit 3' TERM INT HUP\necho $$ > '{_temp.Path}/agent-pid'\nwhile :; do sleep 0.1; done\n");

        using var init = Run(Init(socat), agent);
        var agentPid = await PidFile("agent-pid");
        using (var kill = Process.Start("/bin/kill", ["-" + signal, init.Id.ToString(CultureInfo.InvariantCulture)]))
        {
            await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
        }

        await init.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(3, init.ExitCode);
        Assert.Equal("got-term", (await File.ReadAllTextAsync(Path.Combine(_temp.Path, "agent-term"), TestContext.Current.CancellationToken)).Trim());
        Assert.False(Alive(agentPid));
        var pids = (await File.ReadAllLinesAsync(Path.Combine(_temp.Path, "socat-pids"), TestContext.Current.CancellationToken)).Select(l => int.Parse(l, CultureInfo.InvariantCulture)).ToArray();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(pids, Alive);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_socat_that_dies_at_start_exits_70_and_the_agent_never_runs()
    {
        var socat = FakeSocat("echo 'address already in use'\nexit 1\n");
        var agent = Write("agent", $"#!/bin/sh\necho ran > '{_temp.Path}/agent-ran'\n");

        using var init = Run(Init(socat), agent);
        await init.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(EgressInit.ForwarderExitCode, init.ExitCode);
        Assert.False(File.Exists(Path.Combine(_temp.Path, "agent-ran")));
        Assert.Contains("address already in use", await File.ReadAllTextAsync(Path.Combine(_temp.Path, "socat.log"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(string.Empty, await init.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Contains("forwarder", await init.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_rendered_init_with_the_real_socat_carries_a_call_to_sindri()
    {
        var socat = Which("socat");
        Assert.SkipWhen(socat is null, "socat is not installed");
        using var stub = new TcpListener(IPAddress.Loopback, 0);
        stub.Start();
#pragma warning disable CA2025 // the task is awaited before the test ends; the listener is stopped first so it can finish
        var served = Serve(stub, TestContext.Current.CancellationToken);
#pragma warning restore CA2025
        var net = Path.Combine(_temp.Path, "net");
        var service = new EgressService(new FakeEgressResolver(new Dictionary<string, IPAddress[]>(StringComparer.Ordinal)), new FakeEgressDialer(), n => n == SandboxEnvironment.Socat ? socat : null, Path.Combine(_temp.Path, "run"));
        await using var running = service.Start(new PlanNetwork(NetworkMode.Enforce, [], ((IPEndPoint)stub.LocalEndpoint).Port, 0, net), EgressCaps.Default).Value;
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var sindriPort = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();
        var network = new PlanNetwork(NetworkMode.Enforce, [], sindriPort, ((IPEndPoint)stub.LocalEndpoint).Port, net);
        var init = Write("init.sh", EgressInit.Render(network with { ProxyPort = FreePort() }, socat, Path.Combine(_temp.Path, "socat.log"), net));
        var agent = Write("agent", $"#!/bin/sh\nfor i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20; do\n  out=$(printf 'GET /sindri/mcp HTTP/1.0\\r\\n\\r\\n' | '{socat}' - TCP:127.0.0.1:{sindriPort.ToString(CultureInfo.InvariantCulture)} 2>/dev/null) && [ -n \"$out\" ] && break\n  sleep 0.1\ndone\nprintf '%s' \"$out\" > '{_temp.Path}/answer'\n");

        using var process = Run(init, agent);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        stub.Stop();

        Assert.Equal(0, process.ExitCode);
        Assert.EndsWith("mcp-ok", await File.ReadAllTextAsync(Path.Combine(_temp.Path, "answer"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        await served.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("/bin/sh")]
    [InlineData("dash")]
    [InlineData("bash")]
    public async Task The_agent_reads_the_terminal_input_the_init_got(string shell)
    {
        var resolved = shell.StartsWith('/') ? shell : Which(shell);
        Assert.SkipWhen(resolved is null, shell + " is not installed");
        var socat = FakeSocat("exec sleep 30\n");
        var agent = Write("agent", $"#!/bin/sh\ncat > '{_temp.Path}/stdin'\n");

        using var init = RunWith(resolved, Init(socat), agent);
        await init.StandardInput.WriteAsync("hello".AsMemory(), TestContext.Current.CancellationToken);
        init.StandardInput.Close();
        await init.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(0, init.ExitCode);
        Assert.Equal("hello", await File.ReadAllTextAsync(Path.Combine(_temp.Path, "stdin"), TestContext.Current.CancellationToken));
    }

    private static bool SignalIgnoredHere(int signal)
    {
        var line = File.ReadLines("/proc/self/status").FirstOrDefault(l => l.StartsWith("SigIgn:", StringComparison.Ordinal));
        return line is not null && ulong.TryParse(line["SigIgn:".Length..].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var mask) && (mask & (1UL << (signal - 1))) != 0;
    }

    private static int FreePort()
    {
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var port = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();
        return port;
    }

    private static string? Which(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(d => Path.Combine(d, name)).FirstOrDefault(File.Exists);

    private static async Task Serve(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            var stream = client.GetStream();
            _ = await stream.ReadAsync(new byte[4096], cancellationToken);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.0 200 OK\r\nContent-Length: 6\r\n\r\nmcp-ok"), cancellationToken);
        }
        catch (Exception e) when (e is ObjectDisposedException or SocketException or OperationCanceledException)
        {
        }
    }
}
