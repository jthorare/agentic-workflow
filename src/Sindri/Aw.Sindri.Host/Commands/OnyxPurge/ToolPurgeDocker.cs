using Aw.Kernel;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Host.Commands.OnyxPurge;

internal sealed class ToolPurgeDocker(IToolRunner runner) : IPurgeDocker
{
    public Result<IReadOnlyList<string>> ContainersOf(string project) => throw new NotSupportedException(runner.ToString());

    public Result<IReadOnlyList<string>> VolumesOf(string project) => throw new NotSupportedException();

    public Result<Unit> RemoveContainers(IReadOnlyList<string> names) => throw new NotSupportedException();

    public Result<Unit> RemoveVolumes(IReadOnlyList<string> names) => throw new NotSupportedException();
}
