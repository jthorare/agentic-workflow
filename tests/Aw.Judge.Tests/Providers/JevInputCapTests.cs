using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

// Jev's window is 64k tokens: a long brief goes whole, and over the cap only the longest strings are cut (#1447).
[Lane(TestLane.Unit)]
public sealed class JevInputCapTests
{
    private const string Key = "jev-test-key";

    private const string Answer = """{"answers":{"decision":{"type":"choice","choice":"batch","confidence":0.82}}}""";

    [Fact]
    public async Task A_long_brief_reaches_jev_whole_when_it_fits_the_window()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var input = new JsonObject { ["brief"] = new string('b', 60_000), ["entry"] = "the entry fact" };

        _ = await provider.Decide(Questions.Text, input, Questions.Budget, TestContext.Current.CancellationToken);

        var state = StateSent(handler);
        Assert.Equal(60_000, state["brief"]!.GetValue<string>().Length);
        Assert.DoesNotContain("[truncated", state["brief"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("the entry fact", state["entry"]!.GetValue<string>());
    }

    [Fact]
    public async Task Over_the_cap_only_the_longest_string_is_cut()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        using var provider = new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor());
        var question = Questions.Text with { InputCapChars = 20_000 };
        var input = new JsonObject { ["brief"] = new string('b', 60_000), ["entry"] = new string('e', 500) };

        _ = await provider.Decide(question, input, Questions.Budget, TestContext.Current.CancellationToken);

        var state = StateSent(handler);
        var brief = state["brief"]!.GetValue<string>();
        Assert.Equal(new string('e', 500), state["entry"]!.GetValue<string>());
        Assert.Contains("[truncated", brief, StringComparison.Ordinal);
        Assert.True(brief.Length > 1_000, $"the brief kept {brief.Length} chars");
        Assert.True(state.ToJsonString().Length <= 20_000, $"the state is {state.ToJsonString().Length} chars");
    }

    [Fact]
    public async Task The_cap_comes_from_the_judge_config()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        var providers = Parse("""{"questions":{},"providers":{"inputCapChars":20000}}""").Value.Providers;
        var jev = Lineup(handler, providers);
        var input = new JsonObject { ["brief"] = new string('b', 60_000), ["entry"] = "the entry fact" };

        _ = await jev.Decide(Questions.Text, input, Questions.Budget, TestContext.Current.CancellationToken);

        var state = StateSent(handler);
        Assert.Equal("the entry fact", state["entry"]!.GetValue<string>());
        Assert.InRange(state["brief"]!.GetValue<string>().Length, 1_001, 20_000);
        Assert.True(state.ToJsonString().Length <= 20_000, $"the state is {state.ToJsonString().Length} chars");
    }

    [Fact]
    public async Task Without_a_config_value_the_documented_default_of_160000_chars_applies()
    {
        var handler = FakeHttpHandler.Answering(HttpStatusCode.OK, Answer);
        var jev = Lineup(handler, providers: null);
        var input = new JsonObject { ["brief"] = new string('b', 200_000) };

        _ = await jev.Decide(Questions.Text, input, Questions.Budget, TestContext.Current.CancellationToken);

        var length = StateSent(handler).ToJsonString().Length;
        Assert.InRange(length, 150_000, 160_000);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(256_001)]
    public void A_cap_outside_1000_to_256000_makes_the_config_invalid(int cap) =>
        Assert.Equal(
            JudgeErrors.ConfigMalformed.Code,
            Parse(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{{\"questions\":{{}},\"providers\":{{\"inputCapChars\":{cap}}}}}")).Error.Code);

    private static JsonNode StateSent(FakeHttpHandler handler) =>
        JsonNode.Parse(Assert.Single(handler.Requests).Body)!["state"]!;   // why: the provider always sends a JSON body with a state

    private static IModelProvider Lineup(FakeHttpHandler handler, ProvidersConfig? providers)
    {
        var catalog = new ProviderCatalog(
            _ => null,
            new FakeExecutableLocator([]),
            [new JevProvider(new HttpClient(handler), Secrets(), new FakeInputRedactor())],
            adjudicator: null);
        var rules = new ScriptedProvider(ProviderName.Rules, [.. ContentClasses.All]);
        return catalog.Lineup(providers, allowed: null, rules).Providers.Single(p => p.Name == ProviderName.Jev);
    }

    private static Result<JudgeConfig> Parse(string text) =>
        JudgeConfigParser.Parse(Encoding.UTF8.GetBytes(text), AwJson.CreateOptions(JudgeJsonContext.Default));

    private static FakeSecretSource Secrets() => new(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = Key });
}
