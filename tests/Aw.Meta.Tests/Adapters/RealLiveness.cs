using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Processes;

namespace Aw.Meta.Tests.Adapters;

/// <summary>The real IProcessLiveness for the OS the tests run on, for the integration tests that use real processes.</summary>
internal static class RealLiveness
{
    internal static IProcessLiveness Create() =>
        OperatingSystem.IsMacOS()
            ? new MacProcessLiveness(new MacHostReader(Path.GetTempPath(), new SystemCommandRunner(), MacToolPaths.Default, DiskSpace.Free), TimeProvider.System)
            : new ProcProcessLiveness(TimeProvider.System);
}
