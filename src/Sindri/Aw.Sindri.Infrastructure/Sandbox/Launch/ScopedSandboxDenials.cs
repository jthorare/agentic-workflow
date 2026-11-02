using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// ISandboxDenials for the singleton launcher side: LaunchSessionEvents is scoped (it holds the lease), so the registration hands in an append that runs
/// each event in its own scope, as the launcher's EndLive does.
/// </summary>
/// <param name="append">Appends one event in a fresh scope.</param>
internal sealed class ScopedSandboxDenials(Func<SandboxDenied, CancellationToken, ValueTask<Result<Unit>>> append) : ISandboxDenials
{
    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Append(SandboxDenied denied, CancellationToken cancellationToken) => append(denied, cancellationToken);
}
