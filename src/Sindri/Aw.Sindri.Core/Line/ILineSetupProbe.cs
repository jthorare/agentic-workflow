namespace Aw.Sindri.Core.Line;

/// <summary>Looks at a checkout and the environment for 'line doctor'. Read-only.</summary>
public interface ILineSetupProbe
{
    /// <summary>Reads the facts.</summary>
    /// <param name="checkout">The repo's checkout.</param>
    /// <param name="credentialVariable">The environment variable the forge credential comes from; null when it is a command.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts.</returns>
    public ValueTask<LineSetupFacts> Read(string checkout, string? credentialVariable, CancellationToken cancellationToken);
}
