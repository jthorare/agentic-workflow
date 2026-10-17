using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.Launching;

/// <summary>A launcher that notes every launch that starts while <paramref name="safe"/> says automation is running beside it.</summary>
internal sealed class GuardedLauncher(ILauncher inner, Func<bool> safe) : ILauncher
{
    private int _violations;

    public int Violations => _violations;

    public ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken)
    {
        if (!safe())
        {
            _ = Interlocked.Increment(ref _violations);
        }

        return inner.Launch(request, cancellationToken);
    }

    public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken) => inner.Stop(launch, cancellationToken);

    public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
}
