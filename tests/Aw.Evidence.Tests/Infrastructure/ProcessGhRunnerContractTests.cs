using Aw.Evidence.Infrastructure.Publishing;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real gh runner against a stub gh (a script that answers what the program says).</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessGhRunnerContractTests : GhRunnerContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override Task<GhUnderTest> ArrangeAsync(GhProgram program)
    {
        var body = $"printf '%s' '{program.Stdout}'; printf '%s' '{program.Stderr}' >&2; exit {program.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return Task.FromResult(new GhUnderTest(new ProcessGhRunner(StubCommand.Write(_root, "gh", body))));
    }

    protected override Task<GhUnderTest> ArrangeMissingAsync() =>
        Task.FromResult(new GhUnderTest(new ProcessGhRunner(_root.Combine("no-such-gh"))));

    public void Dispose() => _root.Dispose();
}
