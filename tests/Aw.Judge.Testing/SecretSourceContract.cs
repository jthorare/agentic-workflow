using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Secrets;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The ISecretSource contract: <see cref="FakeSecretSource"/> and the environment-then-keychain source both pass it.</summary>
public abstract class SecretSourceContract
{
    /// <summary>A source in which exactly these secrets are stored.</summary>
    /// <param name="stored">The secrets.</param>
    /// <returns>The source.</returns>
    protected abstract ISecretSource Create(IReadOnlyDictionary<SecretName, string> stored);

    /// <summary>A stored secret is read back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_stored_secret_is_read()
    {
        var source = Create(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = "contract-key-value" });

        var value = await source.Read(SecretName.TypeSafeApiKey, CancellationToken.None);

        Expect.That(value == "contract-key-value", $"the stored secret is read, got '{value}'");
    }

    /// <summary>A secret that isn't stored is null, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_secret_is_null()
    {
        var value = await Create(new Dictionary<SecretName, string>()).Read(SecretName.TypeSafeApiKey, CancellationToken.None);

        Expect.That(value is null, $"a missing secret is null, got '{value}'");
    }

    /// <summary>An empty secret counts as unset.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_secret_is_null()
    {
        var source = Create(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = "" });

        var value = await source.Read(SecretName.TypeSafeApiKey, CancellationToken.None);

        Expect.That(value is null, $"an empty secret is null, got '{value}'");
    }
}
