using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The macOS sources against the real ps, lsof and argument block of this machine; skipped everywhere else.</summary>
[Lane(TestLane.Integration)]
public sealed class MacSourcesIntegrationTests
{
    private static MacProcessTable Table()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "the macOS sources run on macOS");
        return MacSessionSources.Table();
    }

    [Fact]
    public void The_table_finds_this_test_process_with_its_start_cwd_and_files()
    {
        var table = Table();
        var self = Environment.ProcessId;

        var entry = table.Read(TestContext.Current.CancellationToken).Value.Single(p => p.Pid == self);
        var started = new DateTimeOffset(Process.GetCurrentProcess().StartTime).ToUnixTimeSeconds();

        Assert.InRange(entry.StartTicks, started - 2, started + 2);
        Assert.NotNull(entry.Argv0);
        Assert.Equal(Path.GetFullPath(Environment.CurrentDirectory), Path.GetFullPath(table.Cwd(self) ?? string.Empty));
        Assert.NotEmpty(table.OpenFiles(self));
    }

    [Fact]
    public void A_child_with_a_planted_secret_shows_only_its_allowlisted_names()
    {
        var table = Table();
        var info = new ProcessStartInfo("sleep") { UseShellExecute = false };
        info.ArgumentList.Add("120");
        info.Environment["TERM_PROGRAM"] = "WarpTerminal";
        info.Environment["ANTHROPIC_API_KEY"] = ProcessTableContract.PlantedToken;
        using var child = Process.Start(info) ?? throw new InvalidOperationException("sleep did not start");
        try
        {
            var environment = table.Environment(child.Id);

            Assert.Equal("WarpTerminal", environment["TERM_PROGRAM"]);
            Assert.DoesNotContain(environment.Values, static v => v.Contains(ProcessTableContract.PlantedToken, StringComparison.Ordinal));
            Assert.All(environment.Keys, static k => Assert.Contains(k, EnvironmentAllowlist.Names));
        }
        finally
        {
            child.Kill();
            child.WaitForExit();
        }
    }

    [Fact]
    public void A_socket_this_process_listens_on_is_found_through_its_descriptor_and_the_socket_table()
    {
        var table = Table();
        var path = Path.Combine(Path.GetTempPath(), "aw-mac-" + RealTime.NewGuid().ToString("N")[..8] + ".sock");
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        socket.Bind(new System.Net.Sockets.UnixDomainSocketEndPoint(path));
        socket.Listen(1);
        try
        {
            var sockets = MacSessionSources.Sockets().Read();
            var keys = table.OpenFiles(Environment.ProcessId).Select(static f => f.StartsWith("socket:[", StringComparison.Ordinal) && long.TryParse(f.AsSpan(8, f.Length - 9), out var key) ? (long?)key : null).OfType<long>();

            Assert.Contains(keys, key => sockets.TryGetValue(key, out var found) && found.EndsWith(Path.GetFileName(path), StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
