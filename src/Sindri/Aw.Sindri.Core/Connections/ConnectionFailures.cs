using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// What a failed connection call may show. A provider, the keychain or a probe can put a credential into an error's free text (a 401 body that repeats
/// the token, a denied-consent note), so connection calls show only the code and its fixed title, never the instance text.
/// </summary>
public static class ConnectionFailures
{
    /// <summary>The error as the caller may see it: the same code and kind, the fixed title.</summary>
    /// <param name="error">The error from a port.</param>
    /// <returns>The error with no free text.</returns>
    public static Error Safe(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.Code == SindriErrors.ConnectionNotFound.Code)
        {
            return error;   // the store's text names only the connection
        }

        return new Error(error.Code, error.Kind, Title(error.Code) ?? $"the step failed ({error.Code})");
    }

    /// <summary>One line saying why a test failed.</summary>
    /// <param name="error">The error from the probe or the secret store.</param>
    /// <returns>The code and its fixed title.</returns>
    public static string Reason(Error error)
    {
        var safe = Safe(error);
        return $"{safe.Message} ({safe.Code})";
    }

    private static string? Title(string code) => code switch
    {
        _ when code == SindriErrors.ConnectionNotFound.Code => SindriErrors.ConnectionNotFound.Title,
        _ when code == SindriErrors.ConnectionNeedsSignIn.Code => SindriErrors.ConnectionNeedsSignIn.Title,
        _ when code == SindriErrors.ConnectionSignInTimeout.Code => SindriErrors.ConnectionSignInTimeout.Title,
        _ when code == SindriErrors.ConnectionStateMismatch.Code => SindriErrors.ConnectionStateMismatch.Title,
        _ when code == SindriErrors.ConnectionRefreshFailed.Code => SindriErrors.ConnectionRefreshFailed.Title,
        _ when code == SindriErrors.ConnectionSecretUnavailable.Code => SindriErrors.ConnectionSecretUnavailable.Title,
        _ when code == SindriErrors.ConnectionNeedsClientId.Code => SindriErrors.ConnectionNeedsClientId.Title,
        _ when code == SindriErrors.ConnectionNameInvalid.Code => SindriErrors.ConnectionNameInvalid.Title,
        _ when code == SindriErrors.ConnectionConsentDenied.Code => SindriErrors.ConnectionConsentDenied.Title,
        _ when code == SindriErrors.ConnectionExists.Code => SindriErrors.ConnectionExists.Title,
        _ => null,
    };
}
