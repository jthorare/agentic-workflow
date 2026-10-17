namespace Aw.Sindri.Core.Onyx;

/// <summary>The container runtime Onyx runs on (Docker with Compose v2). Read-only: it only asks.</summary>
public interface IContainerRuntime
{
    /// <summary>Reads the runtime's facts. Never throws for a missing, stopped or hung runtime: that is what the facts say.</summary>
    /// <param name="cancellationToken">Cancels the inspection.</param>
    /// <returns>The facts.</returns>
    public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken);
}
