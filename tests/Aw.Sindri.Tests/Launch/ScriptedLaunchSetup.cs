using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>An <see cref="ILaunchSetup"/> that answers what it is given, for the verb tests.</summary>
internal sealed class ScriptedLaunchSetup(Result<LaunchInitReport> init, Result<LaunchShowReport> show) : ILaunchSetup
{
    public LaunchInitRequest? Asked { get; private set; }

    public ValueTask<Result<LaunchInitReport>> Init(LaunchInitRequest request, CancellationToken cancellationToken)
    {
        Asked = request;
        return ValueTask.FromResult(init);
    }

    public ValueTask<Result<LaunchShowReport>> Show(AgentId? only, CancellationToken cancellationToken) => ValueTask.FromResult(show);
}
