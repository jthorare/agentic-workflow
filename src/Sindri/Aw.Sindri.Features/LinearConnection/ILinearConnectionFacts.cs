using Aw.Kernel;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Where the connection stands, read from the live profile and the secret store. Nothing is sent to Linear.</summary>
internal interface ILinearConnectionFacts
{
    /// <summary>Reads the connection.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The view; the profile's or Context's error when they can't be read.</returns>
    public ValueTask<Result<LinearConnectionView>> Read(CancellationToken cancellationToken);
}
