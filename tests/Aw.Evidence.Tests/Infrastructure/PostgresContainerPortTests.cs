using System.Globalization;
using System.Net.Sockets;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// The throwaway Postgres must start even when another process takes the port it picked before docker binds it (#3412). The race is
/// forced on purpose: a hook takes the chosen port and holds it until the test ends. Heavy lane: it needs docker and the
/// postgres:15.2-alpine image that is already on the box.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class PostgresContainerPortTests
{
    [Fact]
    public async Task The_container_starts_when_another_process_takes_the_chosen_port_first()
    {
        TcpListener? thief = null;
        var heldPort = 0;
        var container = new PostgresContainer
        {
            AfterPortChosen = port =>
            {
                heldPort = port;
                thief = PortThief.TryTake(port);   // null: another process took it first, still the case under test (#3451)
                return ValueTask.CompletedTask;
            },
        };

        try
        {
            await container.InitializeAsync();

            Assert.True(container.Port > 0);
            Assert.NotEqual(heldPort, container.Port);
            Assert.Equal(1, Convert.ToInt32(await container.ScalarAsync("SELECT 1"), CultureInfo.InvariantCulture));
        }
        finally
        {
            await container.DisposeAsync();
            thief?.Stop();
        }
    }
}
