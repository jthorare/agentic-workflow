using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The trust port for an agent with no trust prompt (and the launcher's default): nothing to mark.</summary>
internal sealed class NoAgentTrust : IAgentTrust
{
    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Trust(string directory, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Ok());

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Untrust(string directory, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Ok());
}
