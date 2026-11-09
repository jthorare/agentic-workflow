using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>The token for `sindri index connect --token-env NAME`: read from that environment variable instead of typed at a terminal, so an agent session with no terminal can connect a source. The value is never echoed or put in an error.</summary>
/// <param name="value">What the variable holds.</param>
internal sealed class EnvSecretPrompts(string value) : ISecretPrompts
{
    /// <inheritdoc />
    public bool Interactive => true;

    /// <inheritdoc />
    public ValueTask<string?> AskSecret(string question, CancellationToken cancellationToken) => ValueTask.FromResult<string?>(value);
}
