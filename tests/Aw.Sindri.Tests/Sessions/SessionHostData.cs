using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Host detection wired to empty doubles, for tests that are not about hosts.</summary>
internal static class SessionHostData
{
    public static SessionHosts None(IProcessTable processes) =>
        new(processes, new FakeUnixSockets(), new FakeTmuxServers(), new FakeTeamNames(), new FakeTerminalNames(), new FakeLaunchFiles());

    public static Dictionary<string, string> Env(params (string Name, string Value)[] pairs) => pairs.ToDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal);
}
