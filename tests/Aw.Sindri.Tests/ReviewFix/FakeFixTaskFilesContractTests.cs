using Aw.Kernel.Testing;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReviewFix;

[Lane(TestLane.Unit)]
public sealed class FakeFixTaskFilesContractTests : FixTaskFilesContract
{
    private readonly FakeFixTaskFiles _fake = new();

    protected override IFixTaskFiles Files => _fake;

    protected override IReadOnlyDictionary<string, int> Open(string head) => _fake.Heads.TryGetValue(head, out var tasks) ? tasks : new Dictionary<string, int>(StringComparer.Ordinal);
}
