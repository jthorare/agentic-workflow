using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Secrets;

[Lane(TestLane.Unit)]
public sealed class FakeSecretSourceContractTests : SecretSourceContract
{
    protected override ISecretSource Create(IReadOnlyDictionary<SecretName, string> stored) => new FakeSecretSource(stored);
}
