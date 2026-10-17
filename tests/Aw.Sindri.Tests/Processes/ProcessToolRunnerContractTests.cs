using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Processes;

// The real runner over harmless system tools (printf, sh, sleep); never security or op.
[Lane(TestLane.Integration)]
public sealed class ProcessToolRunnerContractTests : ToolRunnerContract
{
    internal override IToolRunner Create(ToolRun scripted, IReadOnlyList<string> standardErrorLines) => new ProcessToolRunner();
}
