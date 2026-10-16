using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Notifications;

[Lane(TestLane.Unit)]
public sealed class InMemoryDeskNoticesContractTests : DeskNoticesContract
{
    protected override IDeskNotices Create() => new InMemoryDeskNotices();
}
