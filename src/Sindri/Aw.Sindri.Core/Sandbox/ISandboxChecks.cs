namespace Aw.Sindri.Core.Sandbox;

/// <summary>Runs the probe, the form check, the login probes and a fixture plan's self-check together (Check again, <c lang="text">sindri sandbox check</c>, doctor).</summary>
public interface ISandboxChecks
{
    /// <summary>Runs every check; one failing never hides the others.</summary>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>The report.</returns>
    public ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken);
}
