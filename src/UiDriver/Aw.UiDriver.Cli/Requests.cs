using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Cli;

/// <summary>Turns a verb and its flags into the verb's request JSON.</summary>
internal static class Requests
{
    /// <summary>The selector the flags give, or null when they give none.</summary>
    /// <param name="a">The arguments.</param>
    /// <returns>The selector.</returns>
    internal static Selector? SelectorOf(CliArguments a)
    {
        var selector = new Selector(
            a.Value("--node-id"), a.Value("--automation-id"), a.Value("--name"), a.Value("--type"), a.Value("--text"), a.Value("--contains"), a.Number("--index"));
        return selector.IsEmpty && selector.Index is null ? null : selector;
    }

    /// <summary>Builds the request for <paramref name="a"/>'s verb.</summary>
    /// <param name="a">The arguments.</param>
    /// <returns>The path of the verb on the driver and its request JSON, or AW-UIDRIVER-001 when a required flag is missing or the verb is none.</returns>
    internal static Result<(string Verb, byte[] Body)> For(CliArguments a)
    {
        var selector = SelectorOf(a);
        return a.Verb switch
        {
            "tree" => Json("tree", new TreeRequest(selector, a.Number("--depth"))),
            "search" => Json("search", new SearchRequest(selector ?? new Selector(), a.Number("--limit"))),
            "props" => WithSelector(a, "props", s => Json("props", new PropsRequest(s))),
            "click" => WithSelector(a, "click", s => Json("click", new ClickRequest(s))),
            "type" => a.Value("--value") is { } value ? Json("type", new TypeRequest(value, selector, a.Has("--replace"))) : Missing("type", "--value"),
            "key" => a.Value("--chord") is { } chord ? Json("key", new KeyRequest(chord, selector)) : Missing("key", "--chord"),
            "scroll" => Json("scroll", new ScrollRequest(a.Real("--dx") ?? 0, a.Real("--dy") ?? 0, selector)),
            "wait-for" => WithSelector(a, "wait-for", s => Json("wait-for", new WaitForRequest(s, a.Value("--state"), a.Value("--expect-text"), a.Number("--wait-ms")))),
            "screenshot" => Json("screenshot", new ScreenshotRequest(selector)),
            "window" => Json("window", new WindowRequest(a.Number("--width"), a.Number("--height"))),
            "action" => WithSelector(a, "action", s => a.Value("--do") is { } action ? Json("action", new ActionRequest(s, action)) : Missing("action", "--do")),
            "call" => a.Value("--path") is { } path && a.Value("--json") is { } text ? (path, Encoding.UTF8.GetBytes(text)) : Missing("call", "--path and --json"),
            _ => UiDriverErrors.Invalid.With($"'{a.Verb}' is not a verb"),
        };
    }

    private static Result<(string Verb, byte[] Body)> WithSelector(CliArguments a, string verb, Func<Selector, Result<(string Verb, byte[] Body)>> build) =>
        SelectorOf(a) is { } selector ? build(selector) : Missing(verb, "a selector flag (--node-id, --automation-id, --name, --type, --text, --contains)");

    private static Result<(string Verb, byte[] Body)> Missing(string verb, string flag) => UiDriverErrors.Invalid.With($"{verb} needs {flag}");

    private static Result<(string Verb, byte[] Body)> Json<T>(string verb, T request)
        where T : notnull => (verb, AwJson.Serialize(request, UiDriverJson.Options));
}
