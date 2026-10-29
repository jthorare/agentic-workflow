using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>A secret store in memory; also answers as the pointer resolver for what it stored.</summary>
public sealed class FakeSecretWriter : ISecretWriter, ISecretPointers
{
    /// <summary>Gets the secrets by pointer text.</summary>
    public IDictionary<string, string> Stored { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets a value indicating whether storing fails (SND-SECRET-004).</summary>
    public bool Fails { get; set; }

    /// <inheritdoc />
    public Result<SecretPointer> Store(string service, string account, string secret)
    {
        if (Fails)
        {
            return SindriErrors.SecretNotStored.With("no store took the key");
        }

        var pointer = SecretPointer.Parse($"keychain:{service}/{account}").Value;
        Stored[pointer.ToString()] = secret;
        return pointer;
    }

    /// <inheritdoc />
    public Result<SecretPointer> StoreInFile(string service, string account, string secret)
    {
        if (Fails)
        {
            return SindriErrors.SecretNotStored.With("no store took the key");
        }

        var pointer = SecretPointer.Parse($"file:/fake-secrets/{service}-{account}").Value;
        Stored[pointer.ToString()] = secret;
        return pointer;
    }

    /// <inheritdoc />
    public Result<string> Resolve(SecretPointer secretPointer)
        => Stored.TryGetValue(secretPointer.ToString(), out var value) ? value : SindriErrors.SecretUnresolved.With("not stored");
}
