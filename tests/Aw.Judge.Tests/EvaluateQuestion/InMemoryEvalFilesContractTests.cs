using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.EvaluateQuestion;

[Lane(TestLane.Unit)]
public sealed class InMemoryEvalFilesContractTests : EvalFilesContract
{
    protected override IEvalFiles Create() => new InMemoryEvalFiles();

    protected override string ScratchPath(string name) => "/scratch/" + name;
}
