using Aw.Kernel;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Host.Commands.OnyxPurge;

internal sealed class ToolPurgeKeychain(IToolRunner runner) : IPurgeKeychain
{
    public Result<Unit> Remove(string service, string account) => throw new NotSupportedException(runner.ToString());
}
