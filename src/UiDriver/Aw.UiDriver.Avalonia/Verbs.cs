using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia;

/// <summary>The verb table: a name to the request type it reads, the <see cref="DriverHost"/> method it calls and how its answer is written.</summary>
internal static class Verbs
{
    /// <summary>The verbs' names, for the CLI's help and the tests.</summary>
    internal static IReadOnlyList<string> Names { get; } = ["tree", "search", "props", "click", "type", "key", "scroll", "wait-for", "screenshot", "window", "action"];

    /// <summary>Runs one verb.</summary>
    /// <param name="verb">The verb's name.</param>
    /// <param name="body">The request's JSON.</param>
    /// <param name="host">The verbs' implementation.</param>
    /// <param name="cancellationToken">Cancels a wait.</param>
    /// <returns>The answer: JSON, or a PNG for a screenshot; a failure is a <see cref="DriverFailure"/> with the status its kind gives.</returns>
    internal static async Task<Reply> RunAsync(string verb, byte[] body, DriverHost host, CancellationToken cancellationToken) => verb switch
    {
        "tree" => await Json<TreeRequest, TreeResponse>(body, host.Tree).ConfigureAwait(false),
        "search" => await Json<SearchRequest, SearchResponse>(body, host.Search).ConfigureAwait(false),
        "props" => await Json<PropsRequest, PropsResponse>(body, host.Props).ConfigureAwait(false),
        "click" => await Json<ClickRequest, ActedResponse>(body, host.Click).ConfigureAwait(false),
        "type" => await Json<TypeRequest, ActedResponse>(body, host.Type).ConfigureAwait(false),
        "key" => await Json<KeyRequest, ActedResponse>(body, host.Key).ConfigureAwait(false),
        "scroll" => await Json<ScrollRequest, ActedResponse>(body, host.Scroll).ConfigureAwait(false),
        "action" => await Json<ActionRequest, ActedResponse>(body, host.Action).ConfigureAwait(false),
        "window" => await Json<WindowRequest, WindowResponse>(body, host.WindowSize).ConfigureAwait(false),
        "wait-for" => await Json<WaitForRequest, WaitedResponse>(body, request => host.WaitFor(request, cancellationToken)).ConfigureAwait(false),
        "screenshot" => await Picture(body, host).ConfigureAwait(false),
        _ => DriverServer.Failure(UiDriverErrors.Invalid.With($"'{verb}' is not a verb ({string.Join(", ", Names)})")),
    };

    private static async Task<Reply> Json<TRequest, TResponse>(byte[] body, Func<TRequest, Task<Result<TResponse>>> verb)
        where TRequest : notnull
        where TResponse : notnull
    {
        var request = AwJson.Deserialize<TRequest>(body.Length == 0 ? "{}"u8 : body, UiDriverJson.Options);
        if (request.IsError)
        {
            return DriverServer.Failure(UiDriverErrors.Invalid.With(request.Error.Message));
        }

        var answer = await verb(request.Value).ConfigureAwait(false);
        return answer.IsError ? DriverServer.Failure(answer.Error) : new Reply(200, "application/json", AwJson.Serialize(answer.Value, UiDriverJson.Options));
    }

    private static async Task<Reply> Picture(byte[] body, DriverHost host)
    {
        var request = AwJson.Deserialize<ScreenshotRequest>(body.Length == 0 ? "{}"u8 : body, UiDriverJson.Options);
        if (request.IsError)
        {
            return DriverServer.Failure(UiDriverErrors.Invalid.With(request.Error.Message));
        }

        var picture = await host.Screenshot(request.Value).ConfigureAwait(false);
        return picture.IsError ? DriverServer.Failure(picture.Error) : new Reply(200, "image/png", picture.Value);
    }
}
