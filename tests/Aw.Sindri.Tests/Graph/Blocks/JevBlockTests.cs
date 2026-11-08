using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Workflow.Jev;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class JevBlockTests
{
    private const string Answer = """{"answers":{"mood":{"type":"choice","choice":"calm","confidence":0.9,"probabilities":{"calm":0.9,"angry":0.1}},"urgency":{"type":"score","score":2,"confidence":0.7,"probabilities":{"0":0.1,"1":0.2,"2":0.7}},"safe":{"type":"noul","noul":0.95,"confidence":0.8}},"usage":{"input_tokens":120,"output_tokens":7}}""";

    private static JevBlock Block(StubHttp stub, string? key = "k-test") =>
        new(new JevClient(stub.Client(), _ => ValueTask.FromResult(key), Redactor.Default));

    private static JsonObject Config(string text) => new()
    {
        ["text"] = text,
        ["questions"] = new JsonArray(
            new JsonObject { ["name"] = "mood", ["type"] = "choice", ["instructions"] = "How does the writer feel?", ["criteria"] = new JsonObject { ["calm"] = "relaxed", ["angry"] = "upset" } },
            new JsonObject { ["name"] = "urgency", ["type"] = "score", ["instructions"] = "How urgent is it?", ["criteria"] = new JsonObject { ["0"] = "never", ["1"] = "soon", ["2"] = "now" } },
            new JsonObject { ["name"] = "safe", ["type"] = "noul", ["instructions"] = "Is it safe to ship?" }),
    };

    [Fact]
    public async Task Questions_go_out_redacted_and_capped_at_160000_characters()
    {
        // built at run time so no token-shaped literal sits in the source
        var secret = "gh" + "p_" + new string('a', 36);
        var stub = new StubHttp((_, _) => StubHttp.Json(Answer));

        var result = await Block(stub).Execute(BlockFakes.Context(Config($"deploy token {secret} {new string('x', 200_000)}")), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var body = Assert.Single(stub.Calls).Body!;
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.Contains("[REDACTED", body, StringComparison.Ordinal);
        var state = JsonNode.Parse(body)!["state"]!.ToJsonString();
        Assert.True(state.Length <= 160_000, $"state was {state.Length} characters");
    }

    [Fact]
    public async Task A_missing_key_fails_with_no_api_key_and_a_503_with_http_503()
    {
        var noKey = new StubHttp((_, _) => StubHttp.Json(Answer));
        var down = new StubHttp((_, _) => StubHttp.Json("{}", System.Net.HttpStatusCode.ServiceUnavailable));
        var offline = new StubHttp((_, _) => throw new HttpRequestException("no route"));

        var missing = await Block(noKey, key: null).Execute(BlockFakes.Context(Config("hi")), TestContext.Current.CancellationToken);
        var unavailable = await Block(down).Execute(BlockFakes.Context(Config("hi")), TestContext.Current.CancellationToken);
        var unreachable = await Block(offline).Execute(BlockFakes.Context(Config("hi")), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.GraphBlockFailed.Code, missing.Error.Code);
        Assert.Contains("no-api-key", missing.Error.Message, StringComparison.Ordinal);
        Assert.Empty(noKey.Calls);
        Assert.Contains("http-503", unavailable.Error.Message, StringComparison.Ordinal);
        Assert.Contains("network-error", unreachable.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_question_becomes_one_output_field_with_choice_confidence_and_probabilities()
    {
        var stub = new StubHttp((_, _) => StubHttp.Json(Answer));

        var result = await Block(stub).Execute(BlockFakes.Context(Config("The release looks fine.")), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var mood = result.Value.Outputs["mood"]!.AsObject();
        Assert.Equal("calm", mood["choice"]!.GetValue<string>());
        Assert.Equal(0.9, mood["confidence"]!.GetValue<double>());
        Assert.Equal(0.1, mood["probabilities"]!["angry"]!.GetValue<double>());
        Assert.Equal(2, result.Value.Outputs["urgency"]!["score"]!.GetValue<double>());
        Assert.Equal(0.95, result.Value.Outputs["safe"]!["noul"]!.GetValue<double>());
        Assert.Equal("jev-1.13.0", result.Value.Outputs["model"]!.GetValue<string>());
        Assert.Equal(120, result.Value.Outputs["usage"]!["input_tokens"]!.GetValue<double>());
        Assert.Equal(7, result.Value.Outputs["usage"]!["output_tokens"]!.GetValue<double>());

        using var sent = JsonDocument.Parse(Assert.Single(stub.Calls).Body!);
        var questions = sent.RootElement.GetProperty("questions");
        Assert.Equal("choice", questions.GetProperty("mood").GetProperty("type").GetString());
        Assert.Equal("score", questions.GetProperty("urgency").GetProperty("type").GetString());
        Assert.Equal("noul", questions.GetProperty("safe").GetProperty("type").GetString());
        Assert.Equal("jev-1.13.0", sent.RootElement.GetProperty("model").GetString());
    }
}
