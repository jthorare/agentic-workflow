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
}
