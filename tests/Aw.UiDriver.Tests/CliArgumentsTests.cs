using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.UiDriver.Cli;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Tests;

public sealed class CliArgumentsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_verb_may_come_after_the_global_flags_and_the_flags_are_read()
    {
        var a = CliArguments.Parse(["--dir", "/d", "click", "--automation-id", "save", "--index", "2"]);

        Assert.Equal(("click", "/d", "save", 2), (a!.Verb, a.Value("--dir"), a.Value("--automation-id"), a.Number("--index")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData]
    [InlineData("--dir", "/d")]
    [InlineData("click", "--bogus", "x")]
    [InlineData("click", "--name")]
    [InlineData("click", "--name", "--type", "Button")]
    [InlineData("click", "--name", "a", "--name", "b")]
    [InlineData("tree", "--depth", "deep")]
    [InlineData("scroll", "--dx", "far")]
    public void A_usage_mistake_is_not_parsed(params string[] args) => Assert.Null(CliArguments.Parse(args));

    private static Result<(string Verb, byte[] Body)> Build(params string[] args) => Requests.For(CliArguments.Parse(args)!);

    private static string Text((string Verb, byte[] Body) request) => Encoding.UTF8.GetString(request.Body);

    [Fact, Trait("Lane", "unit")]
    public void A_click_names_its_element_by_the_selector_flags()
    {
        var request = Build("click", "--automation-id", "save", "--type", "Button").Value;

        Assert.Equal(("click", """{"selector":{"nodeId":null,"automationId":"save","name":null,"type":"Button","text":null,"textContains":null,"index":null}}"""), (request.Verb, Text(request)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_other_verbs_take_their_own_flags()
    {
        Assert.Equal(("type", "name", "Ada", true), TypeOf(Build("type", "--automation-id", "name", "--value", "Ada", "--replace").Value));
        Assert.Contains("\"chord\":\"Ctrl+K\"", Text(Build("key", "--chord", "Ctrl+K").Value), StringComparison.Ordinal);
        Assert.Contains("\"dy\":-120", Text(Build("scroll", "--dy", "-120").Value), StringComparison.Ordinal);
        Assert.Contains("\"state\":\"gone\"", Text(Build("wait-for", "--name", "spinner", "--state", "gone", "--wait-ms", "500").Value), StringComparison.Ordinal);
        Assert.Contains("\"width\":1440", Text(Build("window", "--width", "1440", "--height", "900").Value), StringComparison.Ordinal);
        Assert.Contains("\"action\":\"toggle\"", Text(Build("action", "--name", "dark", "--do", "toggle").Value), StringComparison.Ordinal);
        Assert.Equal(("tree", """{"from":null,"depth":3}"""), (Build("tree", "--depth", "3").Value.Verb, Text(Build("tree", "--depth", "3").Value)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_call_verb_sends_the_json_it_is_given_to_the_path_it_names()
    {
        var request = Build("call", "--path", "search", "--json", """{"selector":{"type":"Button"}}""").Value;

        Assert.Equal(("search", """{"selector":{"type":"Button"}}"""), (request.Verb, Text(request)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("click")]
    [InlineData("props")]
    [InlineData("wait-for")]
    [InlineData("type", "--value", "x", "--index", "x")]
    [InlineData("key")]
    [InlineData("action", "--name", "x")]
    [InlineData("call", "--path", "tree")]
    [InlineData("dance")]
    public void A_verb_missing_what_it_needs_is_an_invalid_request(params string[] args)
    {
        var request = CliArguments.Parse(args) is { } a ? Requests.For(a) : UiDriverErrors.Invalid.With("not parsed");

        Assert.Equal(UiDriverErrors.Invalid.Code, request.Error.Code);
    }

    private static (string Verb, string Selector, string Text, bool Replace) TypeOf((string Verb, byte[] Body) request)
    {
        var json = Encoding.UTF8.GetString(request.Body);
        var read = AwJson.Deserialize<TypeRequest>(request.Body, UiDriverJson.Options).Value;
        return (request.Verb, read.Selector?.AutomationId ?? string.Empty, read.Text, json.Contains("\"replace\":true", StringComparison.Ordinal));
    }
}
