using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>No run is parked on the sandbox now that workflow instances are gone; the graph engine's runs report their own parks (issue 2258 PR 7).</summary>
internal sealed class NoSandboxParks : ISandboxParks
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ParkedOnSandbox>> Parked(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<ParkedOnSandbox>>([]);
}
