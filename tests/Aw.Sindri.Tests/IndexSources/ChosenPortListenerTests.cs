using System.Net.Sockets;
using Aw.Kernel.Testing;

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
                thief = PortThief.TryTake(chosen);   // null: another process took it first, still the case under test (#3451)
            });
            listener.Close();

            Assert.True(port > 0);

            // why: with no thief, the other process may let the port go before HttpListener binds, and the listener rightly takes it (#3546)
            Assert.SkipWhen(thief is null, "another process took the chosen port first, and nothing here holds it");
            Assert.NotEqual(heldPort, port);
        }
        finally
        {
            thief?.Stop();
        }
    }
}
