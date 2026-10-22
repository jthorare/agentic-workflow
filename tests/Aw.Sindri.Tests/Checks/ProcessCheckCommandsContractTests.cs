using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Infrastructure.Checks;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing.Checks;

namespace Aw.Sindri.Tests.Checks;

/// <summary>The adapter over real processes (sh, sleep) honours the check-commands contract.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessCheckCommandsContractTests : CheckCommandsContract
{
    private static readonly string _directory = Path.GetTempPath();

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Prints(int exitCode, string output) =>
        (Real(), new CheckInvocation("c", ["sh", "-c", "printf '%s' \"$1\"; exit " + exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture), "sh", output], TimeSpan.FromSeconds(30)), _directory);

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Hangs() =>
        (Real(), new CheckInvocation("c", ["sleep", "30"], TimeSpan.FromMilliseconds(300)), _directory);

    protected override (ICheckCommands Port, CheckInvocation Command, string WorkingDirectory) Missing() =>
        (Real(), new CheckInvocation("c", ["aw-no-such-tool-for-tests"], TimeSpan.FromSeconds(5)), _directory);

    private static ToolCheckCommands Real() => new(new ProcessToolRunner(), new IdentityScrubber(), TimeProvider.System);
}
