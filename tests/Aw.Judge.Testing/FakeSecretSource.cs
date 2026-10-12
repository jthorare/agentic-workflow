using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Secrets;

namespace Aw.Judge.Testing;

/// <summary>The ISecretSource fake: the given secrets are set (an empty one counts as unset); passes SecretSourceContract.</summary>
/// <param name="secrets">The stored secrets.</param>
public sealed class FakeSecretSource(IReadOnlyDictionary<SecretName, string> secrets) : ISecretSource
{
    /// <summary>A source with no secrets.</summary>
    public FakeSecretSource()
        : this(new Dictionary<SecretName, string>())
    {
    }

    /// <summary>How many reads it has served.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public ValueTask<string?> Read(SecretName name, CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(secrets.TryGetValue(name, out var value) && value.Length > 0 ? value : null);
    }
}
