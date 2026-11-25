using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// The app keeps no client call for POST /sindri/workflow/instances/{id}/commands, which the host stopped mapping in #2258 (#2829).
/// Take over goes through POST /sindri/workflow-runs/{run}/nodes/{node}/takeover (<see cref="ISindriApi.TakeOverAgentNode"/>).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RemovedTakeoverRouteTests
{
    private static readonly Assembly _ui = typeof(ISindriApi).Assembly;

    [Fact]
    public void The_client_has_no_call_to_the_removed_instance_commands_route() =>
        Assert.DoesNotContain(typeof(ISindriApi).GetMethods(), m => string.Equals(m.Name, "ApplyCommand", StringComparison.Ordinal));

    [Fact]
    public void No_sender_of_the_old_takeover_command_is_left() =>
        Assert.Null(_ui.GetType("Aw.Sindri.Ui.ViewModels.Takeover.TakeoverSender"));
}
