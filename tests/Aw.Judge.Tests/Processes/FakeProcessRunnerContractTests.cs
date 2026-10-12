using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

[Lane(TestLane.Unit)]
public sealed class FakeProcessRunnerContractTests : ProcessRunnerContract
{
    protected override IProcessRunner Create(IReadOnlyDictionary<string, FakeProgram> programs)
    {
        var runner = new FakeProcessRunner();
        foreach (var (name, program) in programs)
        {
            _ = runner.With(name, program);
        }

        return runner;
    }
}
