using System.Net;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class JevProviderTests
{
    private const string Key = "jev-test-key";

    private const string Answer = """
        {"answers":{"decision":{"type":"choice","choice":"batch","confidence":0.82,"probabilities":{"send":0.1,"batch":0.82,"drop":0.08}}},"usage":{"input_tokens":12,"output_tokens":1}}
        """;

    [Fact]
    public async Task A_choice_is_decided_with_its_confidence_and_probabilities()
    {
        var result = Assert.IsType<ProviderDecided>(await Decide(FakeHttpHandler.Answering(HttpStatusCode.OK, Answer)));

        Assert.Equal(("batch", 0.82, "model"), (result.Decision, result.Confidence, result.ReasonCode));
        Assert.Equal(0.1, result.Probabilities?["send"]);
    }

    [Fact]
    public async Task A_question_that_needs_reasons_is_not_sent_to_jev_which_only_classifies()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var question = Questions.Text with
        {
            Name = "resolution-check",
            ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { [ResolutionCheckQuestion.Reasons] = ResolutionCheckQuestion.StringList() },
        };

        var result = await provider.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderUnavailable("cannot-explain"), result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task It_posts_to_the_api_with_the_bearer_key()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);

        _ = await Decide(handler);

        var (request, _) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", request.RequestUri?.ToString());
        Assert.Equal(("Bearer", Key), (request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter));
    }

    [Fact]
    public async Task The_body_is_the_state_the_model_and_the_choice_question()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);

        _ = await Decide(handler);

        var json = JsonNode.Parse(Assert.Single(handler.Requests).Body)!;   // why: the provider always sends a JSON body
        Assert.Equal(Questions.Input.ToJsonString(), json["state"]?.ToJsonString());
        Assert.Equal(JevProvider.Model, json["model"]?.GetValue<string>());
        var question = json["questions"]?["decision"];
        Assert.Equal("choice", question?["type"]?.GetValue<string>());
        Assert.Equal(Questions.Text.Prompt, question?["instructions"]?.GetValue<string>());
        Assert.Equal("""{"send":"worth waking for","batch":"batch","drop":"drop"}""", question?["criteria"]?.ToJsonString());
    }

    [Fact]
    public async Task The_state_is_the_input_redacted_and_capped_before_it_leaves_the_machine()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var input = new JsonObject { ["text"] = "deploy with " + SampleSecrets.GitHubToken, ["diff"] = new string('d', 100_000) };

        _ = await provider.Decide(Questions.Text, input, Questions.Budget, TestContext.Current.CancellationToken);

        var state = JsonNode.Parse(Assert.Single(handler.Requests).Body)!["state"]!;   // why: the body always carries the state
        Assert.Equal("deploy with " + FakeInputRedactor.Placeholder, state["text"]?.GetValue<string>());
        Assert.True(state.ToJsonString().Length <= 16_000, $"the state is capped like the stored input, got {state.ToJsonString().Length} chars");
    }

    [Fact]
    public async Task A_secret_in_the_input_leaves_the_machine_in_neither_the_state_nor_the_instructions()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, """{"answers":{"decision":{"type":"choice","choice":"ask","confidence":0.9}}}""");
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var tail = "I exported GH_TOKEN=" + SampleSecrets.GitHubToken + " and next I'll open the PR";
        var question = AskCheckQuestion.Definition.ToRef(new AskCheckInput(tail));
        var input = new JsonObject { ["transcriptTail"] = tail };

        _ = await provider.Decide(question, input, Questions.Budget, TestContext.Current.CancellationToken);

        var body = Assert.Single(handler.Requests).Body;
        var json = JsonNode.Parse(body)!;   // why: the provider always sends a JSON body
        Assert.DoesNotContain(SampleSecrets.GitHubToken, body, StringComparison.Ordinal);
        Assert.Contains("GH_TOKEN=" + FakeInputRedactor.Placeholder, json["state"]?["transcriptTail"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("GH_TOKEN=" + FakeInputRedactor.Placeholder, json["questions"]?["decision"]?["instructions"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_question_with_its_own_input_cap_sends_that_much_state_and_instructions()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var question = Questions.Text with { Prompt = new string('p', 60_000), InputCapChars = 150_000 };
        var input = new JsonObject { ["text"] = new string('t', 100_000) };

        _ = await provider.Decide(question, input, Questions.Budget, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(Assert.Single(handler.Requests).Body)!;   // why: the provider always sends a JSON body
        Assert.Equal(100_000, json["state"]!["text"]!.GetValue<string>().Length);
        Assert.Equal(60_000, json["questions"]!["decision"]!["instructions"]!.GetValue<string>().Length);
    }

    [Fact]
    public async Task The_instructions_are_capped_like_the_state()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var question = Questions.Text with { Prompt = new string('p', 100_000) };

        _ = await provider.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        var instructions = JsonNode.Parse(Assert.Single(handler.Requests).Body)!["questions"]?["decision"]?["instructions"]?.GetValue<string>();
        Assert.InRange(instructions?.Length ?? int.MaxValue, 1, 16_100);   // the stored input's cap plus the truncation marker
    }

    [Fact]
    public async Task Without_a_key_it_is_unavailable_and_sends_nothing()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);

        var result = await Decide(handler, new FakeSecretSource());

        Assert.Equal(new ProviderUnavailable("no-api-key"), result);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "http-500")]
    [InlineData(HttpStatusCode.Unauthorized, "http-401")]
    public async Task An_http_error_status_is_errored(HttpStatusCode status, string reason) =>
        Assert.Equal(new ProviderErrored(reason), await Decide(FakeHttpHandler.Answering(status, "{}")));

    [Theory]
    [InlineData("""{"answers":{"decision":{"type":"choice","choice":"wake","confidence":0.9}}}""", "out-of-enum")]
    [InlineData("""{"answers":{"decision":{"type":"score","score":3}}}""", "unparseable-answer")]
    [InlineData("""{"answers":{}}""", "unparseable-answer")]
    [InlineData("<html>bad gateway</html>", "unparseable-answer")]
    public async Task A_malformed_or_out_of_enum_answer_is_errored(string body, string reason) =>
        Assert.Equal(new ProviderErrored(reason), await Decide(FakeHttpHandler.Answering(HttpStatusCode.OK, body)));

    [Fact]
    public async Task Past_the_budget_it_is_unavailable_with_timeout()
    {
        var handler = new FakeHttpHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());

        var result = await provider.Decide(Questions.Text, Questions.Input, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderUnavailable(ProviderUnavailable.Timeout), result);
    }

    [Fact]
    public async Task A_slow_key_read_counts_against_the_budget()
    {
        // With no key in the environment the source asks the keychain, which can sit on its unlock prompt: that wait is
        // inside Jev's budget, not added in front of it.
        using var provider = new JevProvider(new HttpClient(FakeHttpHandler.Answering(HttpStatusCode.OK, Answer)), new SlowSecretSource(TimeSpan.FromSeconds(3)), new FakeInputRedactor());
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var result = await provider.Decide(Questions.Text, Questions.Input, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderUnavailable(ProviderUnavailable.Timeout), result);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"the read stopped at the budget, took {clock.Elapsed}");
    }

    [Fact]
    public async Task An_unreachable_api_is_unavailable() =>
        Assert.Equal(
            new ProviderUnavailable("network-error"),
            await Decide(new FakeHttpHandler((_, _) => throw new HttpRequestException("connection refused"))));

    [Fact]
    public async Task More_outputs_than_the_api_allows_is_errored_without_a_call()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var question = Questions.Text with { Outputs = [.. Enumerable.Range(0, 256).Select(i => $"o{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}")] };

        Assert.Equal(new ProviderErrored("too-many-options"), await provider.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void It_covers_the_text_classes_only()
    {
        using var provider = new JevProvider(new HttpClient(), Secrets(), new FakeInputRedactor());

        Assert.Equal(ContentClasses.Text.Order(), provider.Classes.Order());
    }

    // A key read that takes a while and stops when its token is cancelled, like the keychain's process runner.
    private sealed class SlowSecretSource(TimeSpan delay) : ISecretSource
    {
        public async ValueTask<string?> Read(SecretName name, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return Key;
        }
    }

    private static FakeSecretSource Secrets() => new(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = Key });

    [Fact]
    public async Task Jev_gets_the_call_s_criteria_as_a_choice_question()
    {
        var input = new ChooseInput(
            "Which evidence?",
            new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal) { ["diff"] = System.Text.Json.JsonDocument.Parse("\"M a.ts\"").RootElement },
            [new("ui", "only UI files changed"), new("api", "a route changed")]);
        var question = ChooseQuestion.For(input).ToRef(input);
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, """{"answers":{"decision":{"type":"choice","choice":"api","confidence":0.7,"probabilities":{"ui":0.2,"api":0.7}}}}""");
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());

        var result = await provider.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal("api", Assert.IsType<ProviderDecided>(result).Decision);
        var sent = JsonNode.Parse(Assert.Single(handler.Requests).Body)!["questions"]!["decision"]!;   // why: the provider always sends a JSON body
        Assert.Equal("""{"ui":"only UI files changed","api":"a route changed"}""", sent["criteria"]!.ToJsonString());
    }

    [Theory]
    [InlineData("""{"answers":{"decision":{"type":"choice","choice":"api"}}}""")]
    [InlineData("""{"answers":{"decision":{"type":"choice","choice":"api","confidence":1.5}}}""")]
    [InlineData("""{"answers":{"decision":{"type":"choice","choice":"api","confidence":-0.1}}}""")]
    public async Task A_choose_answer_without_a_usable_confidence_is_errored_not_certain(string body)
    {
        using var provider = new JevProvider(new HttpClient(FakeHttpHandler.Answering(HttpStatusCode.OK, body)), Secrets(), new FakeInputRedactor());
        var question = Questions.Text with { Name = "choose", Outputs = ["ui", "api"], AskConfidence = true };

        var result = await provider.Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderErrored("unparseable-answer"), result);
    }

    [Fact]
    public async Task Other_questions_still_default_a_missing_jev_confidence_to_certain()
    {
        var result = await Decide(FakeHttpHandler.Answering(HttpStatusCode.OK, """{"answers":{"decision":{"type":"choice","choice":"batch"}}}"""));

        Assert.Equal(1.0, Assert.IsType<ProviderDecided>(result).Confidence);
    }

    private static async Task<ProviderResult> Decide(FakeHttpHandler handler, FakeSecretSource? secrets = null)
    {
        using var provider = new JevProvider(new HttpClient(handler), secrets ?? Secrets(), new FakeInputRedactor());
        return await provider.Decide(Questions.Text, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);
    }
}
