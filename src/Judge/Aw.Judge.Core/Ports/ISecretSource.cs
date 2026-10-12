using Aw.Judge.Core.Secrets;

namespace Aw.Judge.Core.Ports;

/// <summary>Reads secrets (API keys) from wherever the user keeps them.</summary>
public interface ISecretSource
{
    /// <summary>Reads a secret. Never throws for a secret that isn't there or a store that can't be read.</summary>
    /// <param name="name">Which secret.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The secret's value, or null when it isn't set anywhere (an empty value counts as unset).</returns>
    public ValueTask<string?> Read(SecretName name, CancellationToken cancellationToken);
}
