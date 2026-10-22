using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryPrCommentMarksContractTests : PrCommentMarksContract
{
    protected override IPrCommentMarks Create() => new InMemoryPrCommentMarks();
}
