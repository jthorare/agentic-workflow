using System.Net;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class JevProviderContractTests : ModelProviderContract
{
    protected override IModelProvider Create() => Answering("send")!;   // why: this override never returns null

    protected override IModelProvider? Answering(string decision) =>
        With(FakeHttpHandler.Answering(HttpStatusCode.OK, Answer(decision)));

    protected override IModelProvider? Failing() => With(FakeHttpHandler.Answering(HttpStatusCode.InternalServerError, "{}"));

    protected override IModelProvider? Hanging() => With(new FakeHttpHandler(async (_, token) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }));

    private static string Answer(string decision) => new JsonObject
    {
        ["answers"] = new JsonObject { ["decision"] = new JsonObject { ["type"] = "choice", ["choice"] = decision, ["confidence"] = 0.9 } },
    }.ToJsonString();

    private static JevProvider With(FakeHttpHandler handler) =>
        new(new HttpClient(handler), new FakeSecretSource(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = "jev-test-key" }), new FakeInputRedactor());
}
