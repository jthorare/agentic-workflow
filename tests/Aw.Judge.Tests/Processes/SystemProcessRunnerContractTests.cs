using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

[Lane(TestLane.Integration)]
public sealed class SystemProcessRunnerContractTests : ProcessRunnerContract
{
    private readonly ShellPrograms _bin = new();

    protected override IProcessRunner Create(IReadOnlyDictionary<string, FakeProgram> programs)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the programs are sh scripts");
        foreach (var (name, program) in programs)
        {
            _ = _bin.Install(name, program);
        }

        return new SystemProcessRunner(_bin.Environment);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bin.Dispose();
        }

        base.Dispose(disposing);
    }
}
