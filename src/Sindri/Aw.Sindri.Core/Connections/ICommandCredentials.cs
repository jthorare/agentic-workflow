namespace Aw.Sindri.Core.Connections;

/// <summary>Runs a `command:` credential (argv, no shell) and reads the token it prints. The token is never logged or kept.</summary>
public interface ICommandCredentials
{
    /// <summary>Runs the credential's command.</summary>
    /// <param name="credential">The credential, such as `command:gh auth token`.</param>
    /// <returns>The token it printed, trimmed; null when it isn't a `command:` credential, the program is missing, it fails or it prints nothing.</returns>
    public string? Token(string credential);
}
