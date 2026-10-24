using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

// IBatchClassifier: the fake the sorter's and trait-check's unit tests use.
[Lane(TestLane.Unit)]
public sealed class FakeBatchClassifierContractTests : BatchClassifierContract
{
    protected override ValueTask<(IBatchClassifier Classifier, Func<int> Reached)> Create(CancellationToken cancellationToken)
    {
        var fake = FakeBatchClassifier.Answering(new Dictionary<string, BatchAnswer>(StringComparer.Ordinal) { ["q"] = new ChoiceBatchAnswer("yes", 0.9, new Dictionary<string, double>(StringComparer.Ordinal)) });
        return ValueTask.FromResult<(IBatchClassifier, Func<int>)>((fake, () => fake.Asked.Count));
    }
}
