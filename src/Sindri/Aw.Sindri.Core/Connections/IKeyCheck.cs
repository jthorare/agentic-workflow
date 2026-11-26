using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// Checks a pasted API key or personal access token with its service before Sindri stores it (#4357): one cheap authenticated call per service (GitHub's user, Slack's auth.test, Linear's
/// viewer). A preset with no check answers <see cref="KeyCheckOutcome.NotApplicable"/>, and the key is stored unchecked.
/// </summary>
public interface IKeyCheck
{
    /// <summary>Asks the preset's service whether it takes the key.</summary>
    /// <param name="preset">The preset the key is for.</param>
    /// <param name="key">The key. Secret: it goes only into the request's Authorization header.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see cref="KeyCheckOutcome.Checked"/> when the service takes the key, <see cref="KeyCheckOutcome.NotApplicable"/> when the preset has no check (nothing was called); SND-CONN-018 when the service refuses it; SND-CONN-019 when the service could not be asked; SND-CONN-020 when it is rate limiting Sindri. No error holds the key or the service's body.</returns>
    public ValueTask<Result<KeyCheckOutcome>> Check(ConnectionPreset preset, string key, CancellationToken cancellationToken);
}
