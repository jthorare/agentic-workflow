using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

// The batched Jev call takes the same input cap as the single call: 160,000 characters, or config.json's providers.inputCapChars (#1447).
[Lane(TestLane.Unit)]
public sealed class JevBatchInputCapTests
{
    private const string Answers = """{"answers":{"touchesUi":{"type":"noul","noul":0.9}}}""";

    private static readonly NoulBatchQuestion _noul = new("touchesUi", "Does the work change the UI?");

    [Fact]
    public async Task The_batch_state_goes_whole_up_to_the_default_cap()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answers);
        using var provider = Provider(handler);
        var state = new JsonObject { ["diff"] = new string('d', 60_000), ["prompt"] = "ship it" };

        _ = await provider.Batch(new BatchRequest(state, [_noul], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        var sent = JsonNode.Parse(Assert.Single(handler.Requests).Body)!["state"]!;   // why: the provider always sends a JSON body with a state
        Assert.Equal(60_000, sent["diff"]!.GetValue<string>().Length);
        Assert.Equal("ship it", sent["prompt"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_batch_classifier_applies_the_configured_cap_and_cuts_only_the_longest_string()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answers);
        using var provider = Provider(handler);
        var config = JudgeConfigParser.Parse(Encoding.UTF8.GetBytes("""{"questions":{},"providers":{"inputCapChars":20000}}"""), AwJson.CreateOptions(JudgeJsonContext.Default)).Value;
        var state = new JsonObject { ["diff"] = new string('d', 60_000), ["prompt"] = "ship it" };

        _ = await new JevBatchClassifier(provider).Classify(new BatchRequest(state, [_noul], TimeSpan.FromSeconds(5)), config.Providers, allowed: null, TestContext.Current.CancellationToken);

        var sent = JsonNode.Parse(Assert.Single(handler.Requests).Body)!["state"]!;   // why: the provider always sends a JSON body with a state
        Assert.Equal("ship it", sent["prompt"]!.GetValue<string>());
        Assert.InRange(sent["diff"]!.GetValue<string>().Length, 1_001, 20_000);
        Assert.True(sent.ToJsonString().Length <= 20_000, $"the state is {sent.ToJsonString().Length} chars");
    }

    private static JevProvider Provider(FakeHttpHandler handler) =>
        new(new HttpClient(handler), new FakeSecretSource(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = "jev-test-key" }), new FakeInputRedactor());
}
