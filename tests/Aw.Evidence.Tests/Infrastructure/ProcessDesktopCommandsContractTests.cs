using Aw.Evidence.Infrastructure.Desktop;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real runner over stub programs: one named with a directory (the repo's own, found against the working directory), as the fixture script is.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessDesktopCommandsContractTests : DesktopCommandsContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override Task<DesktopCommandsUnderTest> ArrangeAsync(CliProgram program)
    {
        _ = Directory.CreateDirectory(_root.Combine("scripts"));
        _ = StubCommand.Write(_root, "scripts/tool.sh", "printf '%s' '" + program.Stdout + "'\nexit " + program.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.FromResult(new DesktopCommandsUnderTest(new ProcessDesktopCommands(), "scripts/tool.sh", _root.Path));
    }

    protected override Task<DesktopCommandsUnderTest> ArrangeMissingAsync() =>
        Task.FromResult(new DesktopCommandsUnderTest(new ProcessDesktopCommands(), "scripts/absent.sh", _root.Path));

    public void Dispose() => _root.Dispose();
}
