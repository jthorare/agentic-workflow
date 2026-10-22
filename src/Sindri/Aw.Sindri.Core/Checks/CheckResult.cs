using System.Globalization;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Checks;

/// <summary>A command and how its run ended.</summary>
/// <param name="Command">What ran.</param>
/// <param name="Run">How it ended.</param>
public sealed record CheckResult(BuildCommand Command, CheckRun Run)
{
    /// <summary>The command's name.</summary>
    public string Name => Command.Name;

    /// <summary>A sentence for a finding: 'dotnet test exited 2' or 'dotnet test ran past its timeout'.</summary>
    public string Summary => Run.TimedOut ? $"{Name} ran past its timeout" : string.Create(CultureInfo.InvariantCulture, $"{Name} exited {Run.ExitCode}");
}
