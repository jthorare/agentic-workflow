using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Questions;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

// What Jev receives for one choose call, state and instructions together, fits the input cap, and the facts are in it once (#1447).
[Lane(TestLane.Unit)]
public sealed class JevChooseOnceTests
{
    private const int Cap = 160_000;

    [Fact]
    public async Task A_150_KB_brief_reaches_jev_once_and_the_whole_body_fits_the_cap()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, """{"answers":{"decision":{"type":"choice","choice":"ui","confidence":0.9}}}""");
        using var provider = new JevProvider(
            new HttpClient(handler),
            new FakeSecretSource(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = "jev-test-key" }),
            new FakeInputRedactor());
        var brief = new string('b', 150_000);
        var input = new ChooseInput(
            "Which evidence does this change need?",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["brief"] = JsonDocument.Parse(JsonSerializer.Serialize(brief)).RootElement },
            [new("ui", "only UI files changed"), new("api", "a route changed")]);
        var question = ChooseQuestion.For(input).ToRef(input);
        var state = new JsonObject
        {
            ["prompt"] = input.Prompt,
            ["state"] = new JsonObject { ["brief"] = brief },
            ["choices"] = new JsonArray(new JsonObject { ["name"] = "ui", ["criterion"] = "only UI files changed" }, new JsonObject { ["name"] = "api", ["criterion"] = "a route changed" }),
        };

        _ = await provider.Decide(question, state, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var body = Assert.Single(handler.Requests).Body;
        Assert.True(body.Length <= Cap, $"jev got {body.Length} chars, the cap is {Cap}");
        Assert.Equal(1, BigRuns(body, 'b'));
    }

    // How many separate runs of the character are at least 10,000 long.
    private static int BigRuns(string text, char c)
    {
        int runs = 0, run = 0;
        foreach (var ch in text)
        {
            run = ch == c ? run + 1 : 0;
            if (run == 10_000)
            {
                runs++;
            }
        }

        return runs;
    }
}
