using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IConnectionSecrets in memory: the tokens by pointer text. A fake keychain for the slices and the sign-in.</summary>
public sealed class FakeConnectionSecrets : IConnectionSecrets
{
    private readonly Dictionary<string, TokenSet> _stored = new(StringComparer.Ordinal);

    /// <summary>Gets or sets a value indicating whether every call fails with SND-CONN-006.</summary>
    public bool Fails { get; set; }

    /// <summary>Gets the pointers whose tokens are stored now.</summary>
    public IReadOnlyCollection<string> Pointers => [.. _stored.Keys];

    /// <summary>Gets the pointers deleted, in order.</summary>
    public IList<string> Deleted { get; } = [];

    /// <summary>Gets the tokens stored under a pointer, or null.</summary>
    /// <param name="location">The pointer text.</param>
    /// <returns>The tokens.</returns>
    public TokenSet? Tokens(string location) => _stored.GetValueOrDefault(location);

    /// <inheritdoc />
    public Result<SecretPointer> Store(ConnectionName name, TokenSet tokens)
    {
        if (Fails)
        {
            return SindriErrors.ConnectionSecretUnavailable.With("the secret store is not available");
        }

        var pointer = SecretPointer.Parse($"keychain:sindri-connection/{name.Service}.{name.Label}").Value;
        _stored[pointer.ToString()] = tokens;
        return pointer;
    }

    /// <inheritdoc />
    public Result<TokenSet> Read(SecretPointer location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return !Fails && _stored.TryGetValue(location.ToString(), out var tokens)
            ? tokens
            : SindriErrors.ConnectionSecretUnavailable.With("the secret could not be read");
    }

    /// <inheritdoc />
    public Result<Unit> Delete(SecretPointer location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (Fails)
        {
            return SindriErrors.ConnectionSecretUnavailable.With("the secret could not be deleted");
        }

        _ = _stored.Remove(location.ToString());
        Deleted.Add(location.ToString());
        return Unit.Value;
    }
}
