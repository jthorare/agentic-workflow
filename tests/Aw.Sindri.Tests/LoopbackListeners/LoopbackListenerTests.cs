using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LoopbackListeners;

/// <summary>The loopback HttpListener fakes in Aw.Sindri.Testing start through one helper that takes a fresh port when the first one is taken (#2387).</summary>
[Lane(TestLane.Integration)]
public sealed partial class LoopbackListenerTests
{
    [GeneratedRegex(@"new\s+HttpListener\s*\(|HttpListener\s+\w+\s*=\s*new\s*\(\s*\)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex OwnListener { get; }

    [Fact]
    public void Start_takes_a_fresh_port_when_the_first_one_is_taken()
    {
        using var held = new TcpListener(IPAddress.Loopback, 0);
        held.Start();
        var heldPort = ((IPEndPoint)held.LocalEndpoint).Port;
        var calls = 0;

        var (listener, baseUri) = LoopbackListener.Start(() => ++calls == 1 ? heldPort : FreePort());
        try
        {
            Assert.True(listener.IsListening);
            Assert.NotEqual(heldPort, baseUri.Port);
            Assert.Equal(2, calls);
        }
        finally
        {
            listener.Close();
        }
    }

    [Fact]
    public void Start_gives_up_after_its_attempts_when_every_port_is_taken()
    {
        using var held = new TcpListener(IPAddress.Loopback, 0);
        held.Start();
        var heldPort = ((IPEndPoint)held.LocalEndpoint).Port;
        var calls = 0;

        _ = Assert.Throws<HttpListenerException>(() =>
        {
            _ = LoopbackListener.Start(() =>
            {
                calls++;
                return heldPort;
            });
        });

        Assert.Equal(LoopbackListener.StartAttempts, calls);
    }

    [Fact]
    public void No_fake_in_Testing_builds_its_own_HttpListener()
    {
        var testing = Path.Combine(RepoRoot(), "tests", "Aw.Sindri.Testing");
        var pattern = OwnListener;
        var sep = Path.DirectorySeparatorChar;
        var hits = new List<string>();

        foreach (var file in Directory.EnumerateFiles(testing, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(testing, file);
            if (Path.GetFileName(file) == "LoopbackListener.cs"
                || relative.StartsWith("bin" + sep, StringComparison.Ordinal)
                || relative.StartsWith("obj" + sep, StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    hits.Add(relative + ":" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        Assert.True(hits.Count == 0, "These files build their own HttpListener instead of LoopbackListener.Start: " + string.Join(", ", hits));
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
