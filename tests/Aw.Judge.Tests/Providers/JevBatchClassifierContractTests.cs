using System.Net;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

// IBatchClassifier: the Jev adapter, over a fake HTTP endpoint that answers question 'q' with 'yes'.
[Lane(TestLane.Unit)]
public sealed class JevBatchClassifierContractTests : BatchClassifierContract
{
    protected override ValueTask<(IBatchClassifier Classifier, Func<int> Reached)> Create(CancellationToken cancellationToken)
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, """{"answers":{"q":{"type":"choice","choice":"yes","confidence":0.9}}}""");
        var jev = new JevProvider(new HttpClient(handler), new FakeSecretSource(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = "jev-test-key" }), new FakeInputRedactor());
        return ValueTask.FromResult<(IBatchClassifier, Func<int>)>((new JevBatchClassifier(jev), () => handler.Requests.Count));
    }
}
