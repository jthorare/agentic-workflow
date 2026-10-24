using System.Net;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

// The shared batched Jev call (cutover unit 6.0): one POST /v1/systemone with several typed questions, against a fake
// HTTP endpoint. The prompt sorter (unit 6.1) and trait-check (unit 6.2) only call it.
[Lane(TestLane.Unit)]
public sealed class JevBatchTests
{
    private const string Key = "jev-test-key";

    private static readonly ChoiceBatchQuestion _choice = new("ambiguity", "How clear is the request?", new Dictionary<string, string>(StringComparer.Ordinal) { ["clear"] = "it is", ["vague"] = "it is not" });
    private static readonly NoulBatchQuestion _noul = new("touchesUi", "Does the work change the UI?");
    private static readonly ScoreBatchQuestion _score = new("complexity", "How much work is it?", ["trivial", "small", "large"]);

    private const string Answers = """{"answers":{"ambiguity":{"type":"choice","choice":"vague","confidence":0.6,"probabilities":{"clear":0.4,"vague":0.6}},"touchesUi":{"type":"noul","noul":0.92},"complexity":{"type":"score","score":2,"confidence":0.7,"probabilities":{"0":0.1,"1":0.2,"2":0.7}}},"usage":{"input_tokens":120,"output_tokens":9}}""";

    [Fact]
    public async Task One_request_carries_every_question_typed_and_the_redacted_state()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answers);
        using var provider = Provider(handler);
        var state = new JsonObject { ["prompt"] = "ship it with " + SampleSecrets.GitHubToken };

        _ = await provider.Batch(new BatchRequest(state, [_choice, _noul, _score], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        var (request, text) = Assert.Single(handler.Requests);
        Assert.Equal(("POST", "https://api.typesafe.ai/v1/systemone", "Bearer", Key), (request.Method.Method, request.RequestUri?.ToString(), request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter));
        var json = JsonNode.Parse(text)!;   // why: the provider always sends a JSON body
        Assert.Equal(JevProvider.Model, json["model"]?.GetValue<string>());
        Assert.Equal("ship it with " + FakeInputRedactor.Placeholder, json["state"]?["prompt"]?.GetValue<string>());
        Assert.DoesNotContain(SampleSecrets.GitHubToken, text, StringComparison.Ordinal);
        var questions = json["questions"]!;   // why: the provider always sends questions
        Assert.Equal(("choice", "How clear is the request?", """{"clear":"it is","vague":"it is not"}"""), Describe(questions["ambiguity"]));
        Assert.Equal(("noul", "Does the work change the UI?", null), Describe(questions["touchesUi"]));
        Assert.Equal(("score", "How much work is it?", """["trivial","small","large"]"""), Describe(questions["complexity"]));
    }

    [Fact]
    public async Task Each_answer_comes_back_under_its_question_with_the_usage()
    {
        using var provider = Provider(FakeHttpHandler.Answering(HttpStatusCode.OK, Answers));

        var result = await provider.Batch(new BatchRequest(State: null, [_choice, _noul, _score], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        var decided = Assert.IsType<BatchDecided>(result);
        var choice = Assert.IsType<ChoiceBatchAnswer>(decided.Answers["ambiguity"]);
        Assert.Equal(("vague", 0.6, 0.4), (choice.Choice, choice.Confidence, choice.Probabilities["clear"]));
        Assert.Equal(0.92, Assert.IsType<NoulBatchAnswer>(decided.Answers["touchesUi"]).Noul);
        var score = Assert.IsType<ScoreBatchAnswer>(decided.Answers["complexity"]);
        Assert.Equal((2.0, 0.7), (score.Score, score.Confidence));
        Assert.Equal(new BatchUsage(120, 9), decided.Usage);
    }

    [Theory]
    [InlineData("""{"answers":{"ambiguity":{"type":"choice","choice":"other"}}}""")]
    [InlineData("""{"answers":{"ambiguity":{"type":"noul","noul":1}}}""")]
    [InlineData("""{"answers":{"ambiguity":{"type":"choice"}}}""")]
    [InlineData("""{"answers":{}}""")]
    [InlineData("""{}""")]
    public async Task A_question_answered_in_an_unusable_shape_is_left_out_not_failed(string body)
    {
        using var provider = Provider(FakeHttpHandler.Answering(HttpStatusCode.OK, body));

        var result = await provider.Batch(new BatchRequest(State: null, [_choice], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        Assert.Empty(Assert.IsType<BatchDecided>(result).Answers);
    }

    [Fact]
    public async Task A_missing_or_out_of_range_confidence_is_certain_as_for_a_single_question()
    {
        const string body = """{"answers":{"ambiguity":{"type":"choice","choice":"clear","confidence":3}}}""";
        using var provider = Provider(FakeHttpHandler.Answering(HttpStatusCode.OK, body));

        var result = await provider.Batch(new BatchRequest(State: null, [_choice], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        Assert.Equal(1.0, Assert.IsType<ChoiceBatchAnswer>(Assert.IsType<BatchDecided>(result).Answers["ambiguity"]).Confidence);
    }

    [Fact]
    public async Task Without_a_key_it_is_unavailable_and_sends_nothing()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answers);
        using var provider = new JevProvider(new HttpClient(handler), new FakeSecretSource(), new FakeInputRedactor());

        var result = await provider.Batch(new BatchRequest(State: null, [_choice], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderUnavailable("no-api-key"), result);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "http-500")]
    [InlineData(HttpStatusCode.Unauthorized, "http-401")]
    public async Task An_http_error_status_is_errored(HttpStatusCode status, string reason)
    {
        using var provider = Provider(FakeHttpHandler.Answering(status, "{}"));

        Assert.Equal(new ProviderErrored(reason), await provider.Batch(new BatchRequest(State: null, [_choice], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_body_that_is_not_an_answer_is_errored()
    {
        using var provider = Provider(FakeHttpHandler.Answering(HttpStatusCode.OK, "<html>bad gateway</html>"));

        Assert.Equal(new ProviderErrored("unparseable-answer"), await provider.Batch(new BatchRequest(State: null, [_choice], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Past_the_budget_it_is_unavailable_with_timeout()
    {
        using var provider = Provider(new FakeHttpHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        var result = await provider.Batch(new BatchRequest(State: null, [_choice], TimeSpan.FromMilliseconds(100)), TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderUnavailable(ProviderUnavailable.Timeout), result);
    }

    [Fact]
    public async Task An_unreachable_api_is_unavailable()
    {
        using var provider = Provider(new FakeHttpHandler((_, _) => throw new HttpRequestException("connection refused")));

        Assert.Equal(new ProviderUnavailable("network-error"), await provider.Batch(new BatchRequest(State: null, [_choice], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_batch_the_api_cant_take_is_errored_without_a_call()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answers);
        using var provider = Provider(handler);
        var tooManyOptions = new ChoiceBatchQuestion("c", "p", Enumerable.Range(0, 256).ToDictionary(i => "o" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), i => "x", StringComparer.Ordinal));
        var tooManyLevels = new ScoreBatchQuestion("s", "p", [.. Enumerable.Range(0, 11).Select(i => "l" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(new ProviderErrored("too-many-options"), await provider.Batch(new BatchRequest(State: null, [tooManyOptions], TimeSpan.FromSeconds(5)), ct));
        Assert.Equal(new ProviderErrored("too-many-levels"), await provider.Batch(new BatchRequest(State: null, [tooManyLevels], TimeSpan.FromSeconds(5)), ct));
        Assert.Equal(new ProviderErrored("duplicate-question"), await provider.Batch(new BatchRequest(State: null, [_noul, _noul], TimeSpan.FromSeconds(5)), ct));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_empty_batch_is_decided_without_a_call()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answers);
        using var provider = Provider(handler);

        var result = await provider.Batch(new BatchRequest(State: null, [], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        Assert.Empty(Assert.IsType<BatchDecided>(result).Answers);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_secret_in_a_question_text_does_not_leave_the_machine()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answers);
        using var provider = Provider(handler);
        var question = new ChoiceBatchQuestion("c", "does it leak " + SampleSecrets.GitHubToken, new Dictionary<string, string>(StringComparer.Ordinal) { ["yes"] = "with " + SampleSecrets.GitHubToken });

        _ = await provider.Batch(new BatchRequest(State: null, [question], TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(SampleSecrets.GitHubToken, Assert.Single(handler.Requests).Body, StringComparison.Ordinal);
    }

    private static (string?, string?, string?) Describe(JsonNode? question) =>
        (question?["type"]?.GetValue<string>(), question?["instructions"]?.GetValue<string>(), question?["criteria"]?.ToJsonString());

    private static JevProvider Provider(FakeHttpHandler handler) =>
        new(new HttpClient(handler), new FakeSecretSource(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = Key }), new FakeInputRedactor());
}
