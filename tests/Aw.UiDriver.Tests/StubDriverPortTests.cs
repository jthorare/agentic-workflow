using System.Net;
using System.Net.Sockets;

namespace Aw.UiDriver.Tests;

public sealed class StubDriverPortTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task The_stub_starts_when_another_process_takes_the_chosen_port_first()
    {
        TcpListener? thief = null;
        var heldPort = 0;
        try
        {
            await using var stub = new StubDriver(port =>
            {
                if (thief is not null)
                {
                    return;
                }

                heldPort = port;
                var taker = new TcpListener(IPAddress.Loopback, port);
                try
                {
                    taker.Start();
                    thief = taker;
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    taker.Dispose();   // another process took it first, still the case under test (#3451); Aw.Kernel.Testing.PortThief isn't referenced here
                }
            });

            Assert.True(stub.Port > 0);
            Assert.NotEqual(heldPort, stub.Port);
        }
        finally
        {
            thief?.Stop();
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_stub_removes_its_directory_and_names_the_count_when_every_port_is_taken()
    {
        var thieves = new List<TcpListener>();
        string? directory = null;
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => new StubDriver(
                port =>
                {
                    var taker = new TcpListener(IPAddress.Loopback, port);
                    taker.Start();
                    thieves.Add(taker);
                },
                made => directory = made));

            Assert.Contains("gave up after 5 ports", error.Message, StringComparison.Ordinal);
            Assert.NotNull(directory);
            Assert.False(Directory.Exists(directory), "the stub's temp directory leaked after the constructor threw");
        }
        finally
        {
            foreach (var thief in thieves)
            {
                thief.Stop();
            }

            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);   // today's leak: do not add to /tmp on the red run
            }
        }
    }
}
