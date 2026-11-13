using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Tests.IndexSources;

public sealed class ChosenPortListenerTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_listener_starts_when_another_process_takes_the_chosen_port_first()
    {
        TcpListener? thief = null;
        var heldPort = 0;
        try
        {
            var (listener, port) = ChosenPortListener.Start(chosen =>
            {
                if (thief is not null)
                {
                    return;
                }

                heldPort = chosen;
                thief = new TcpListener(IPAddress.Loopback, chosen);
                thief.Start();
            });
            listener.Close();

            Assert.True(port > 0);
            Assert.NotEqual(heldPort, port);
        }
        finally
        {
            thief?.Stop();
        }
    }
}
