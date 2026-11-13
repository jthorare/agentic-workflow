using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Aw.Evidence.Core;
using Aw.Evidence.Infrastructure.Stack;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class StackProbeTests : IDisposable
{
    private static readonly DatabaseSettings _db = new(Container: "db_1", User: "app", SeedTable: null);

    private readonly TempDirectory _root = new();

    [Fact]
    public async Task A_page_title_is_read_and_decoded()
    {
        using var server = new LocalHttpServer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/login"] = "<html><head><TITLE lang=\"en\">\n Acme &amp; Co </TITLE></head></html>",
            ["/bare"] = "<html><body>no title</body></html>",
        });
        using var probe = new ProcessStackProbe("docker", _db);

        Assert.Equal("Acme & Co", (await probe.PageTitleAsync(new Uri(server.Origin + "/login"), TestContext.Current.CancellationToken)).Value);
        Assert.Equal(EvidenceErrors.ProbeFailed.Code, (await probe.PageTitleAsync(new Uri(server.Origin + "/bare"), TestContext.Current.CancellationToken)).Error.Code);
        Assert.Equal(EvidenceErrors.ProbeFailed.Code, (await probe.PageTitleAsync(new Uri(server.Origin + "/missing"), TestContext.Current.CancellationToken)).Error.Code);
    }

    [Fact]
    public async Task A_page_nobody_serves_is_a_probe_failure()
    {
        using var probe = new ProcessStackProbe("docker", _db);
        using var closed = LocalHttpServer.ClosedPort(out var port);

        var title = await probe.PageTitleAsync(new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.ProbeFailed.Code, title.Error.Code);
    }

    [Fact]
    public async Task A_port_is_open_only_while_something_listens()
    {
        using var probe = new ProcessStackProbe("docker", _db);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            Assert.True((await probe.PortOpenAsync("127.0.0.1", port, TestContext.Current.CancellationToken)).IsSuccess);
        }
        finally
        {
            listener.Stop();
        }

        // A port bound but never listening: nobody can take it, unlike a freed one (#3241).
        using var closed = LocalHttpServer.ClosedPort(out var closedPort);
        Assert.Equal(EvidenceErrors.ProbeFailed.Code, (await probe.PortOpenAsync("127.0.0.1", closedPort, TestContext.Current.CancellationToken)).Error.Code);
    }

    [Fact]
    public async Task The_database_is_ready_when_pg_isready_in_the_container_says_so()
    {
        var args = _root.Combine("args");
        using var ready = new ProcessStackProbe(StubCommand.Write(_root, "docker-ok", $"printf '%s\\n' \"$@\" > '{args}'; exit 0"), _db);
        using var down = new ProcessStackProbe(StubCommand.Write(_root, "docker-down", "echo 'no response' >&2; exit 2"), _db);
        using var absent = new ProcessStackProbe(_root.Combine("no-such-docker"), _db);

        Assert.True((await ready.DatabaseReadyAsync(TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(["exec", "db_1", "pg_isready", "-U", "app"], await File.ReadAllLinesAsync(args, TestContext.Current.CancellationToken));
        Assert.Contains("no response", (await down.DatabaseReadyAsync(TestContext.Current.CancellationToken)).Error.Message, StringComparison.Ordinal);
        Assert.Equal(EvidenceErrors.ProbeFailed.Code, (await absent.DatabaseReadyAsync(TestContext.Current.CancellationToken)).Error.Code);
    }

    [Fact]
    public async Task With_no_container_configured_there_is_no_database_to_probe_so_docker_never_runs()
    {
        using var probe = new ProcessStackProbe(_root.Combine("no-such-docker"), new DatabaseSettings(Container: null, User: "postgres", SeedTable: null));

        Assert.True((await probe.DatabaseReadyAsync(TestContext.Current.CancellationToken)).IsSuccess);
    }

    public void Dispose() => _root.Dispose();
}
