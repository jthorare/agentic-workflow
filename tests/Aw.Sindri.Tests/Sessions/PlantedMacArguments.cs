using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>An argument block reader whose environment holds a planted secret beside an allowlisted name, for every pid.</summary>
internal sealed class PlantedMacArguments : IMacProcessArguments
{
    public MacArguments? Read(int pid) => pid >= 2_000_000_000 ? null : new MacArguments(
        "claude",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["TERM_PROGRAM"] = "WarpTerminal", ["ANTHROPIC_API_KEY"] = ProcessTableContract.PlantedToken });
}
