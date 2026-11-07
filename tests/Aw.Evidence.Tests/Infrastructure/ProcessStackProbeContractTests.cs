using System.Net;
using System.Net.Sockets;
using Aw.Evidence.Infrastructure.Stack;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real probe against a local HTTP server, a stub docker and a TCP listener on loopback.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessStackProbeContractTests : StackProbeContract, IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly List<IDisposable> _owned = [];

    protected override Task<StackUnderTest> ArrangeAsync(StackState state)
    {
        var page = state.LoginTitle is { } title ? $"<html><head><title>{title}</title></head></html>" : "<html><body>no title</body></html>";
        var server = Own(new LocalHttpServer(new Dictionary<string, string>(StringComparer.Ordinal) { ["/login"] = page }));
        var docker = StubCommand.Write(_root, state.DatabaseReady ? "docker-ready" : "docker-down", state.DatabaseReady ? "exit 0" : "echo 'no response' >&2; exit 2");
        var port = state.PortOpen ? Listening() : Closed();
        var probe = Own(new ProcessStackProbe(docker, new DatabaseSettings(Container: "db", User: "postgres", SeedTable: null)));
        return Task.FromResult(new StackUnderTest(probe, new Uri(server.Origin + "/login"), "127.0.0.1", port));
    }

    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }

        _root.Dispose();
    }

    private int Listening()
    {
        var listener = Own(new TcpListener(IPAddress.Loopback, 0));
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private int Closed()
    {
        _ = Own(LocalHttpServer.ClosedPort(out var port));
        return port;
    }

    private T Own<T>(T disposable)
        where T : IDisposable
    {
        _owned.Add(disposable);
        return disposable;
    }
}
