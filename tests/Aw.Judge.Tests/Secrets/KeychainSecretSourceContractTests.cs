using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Secrets;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Secrets;

/// <summary>Each stored secret is in the environment and in the keychain; the keychain is a fake `security` (the real one is the user's).</summary>
[Lane(TestLane.Unit)]
public sealed class KeychainSecretSourceContractTests : SecretSourceContract
{
    protected override ISecretSource Create(IReadOnlyDictionary<SecretName, string> stored)
    {
        var key = stored.TryGetValue(SecretName.TypeSafeApiKey, out var value) ? value : null;
        var runner = new FakeProcessRunner();
        if (key is not null)
        {
            _ = runner.With("security", new FakeProgram { StandardOutput = key + "\n", ExitCode = key.Length > 0 ? 0 : 44 });
        }

        return new KeychainSecretSource(name => name == "TYPESAFE_API_KEY" ? key : null, runner);
    }
}
