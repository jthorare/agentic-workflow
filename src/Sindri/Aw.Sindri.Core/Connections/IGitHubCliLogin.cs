namespace Aw.Sindri.Core.Connections;

/// <summary>The gh login on this box (first-run spec decision 13): whether `gh auth status` passes for github.com, and the login it names. Reads no token.</summary>
public interface IGitHubCliLogin
{
    /// <summary>Asks gh.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The GitHub login gh is signed in as; null when gh is missing, signed out or names no login.</returns>
    public ValueTask<string?> Login(CancellationToken cancellationToken);
}
