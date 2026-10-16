using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Tests.Callers;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The request and decision calls of the API P4 tests and goldens, on a golden host whose projector the test drives itself: each call first lets the views catch up (a command waits for them), and sets the views up again after.</summary>
internal static class RequestCalls
{
    /// <summary>POST /requests with <paramref name="body"/> as <paramref name="headers"/>; the views are caught up before and after.</summary>
    public static async Task<Answer> File(GoldenHost host, string body, (string Name, string Value)[] headers, CancellationToken ct)
    {
        await ViewScenarios.CatchUp(host, ct);
        var answer = await host.Http.Ask(Post("/sindri/requests", body, headers));
        await ViewScenarios.CatchUp(host, ct);
        return answer;
    }

    /// <summary>A POST /requests body.</summary>
    public static string Body(string kind, string text, string? item = null) =>
        new JsonObject { ["kind"] = kind, ["item"] = item, ["text"] = text }.ToJsonString();

    /// <summary>Files a request as <paramref name="headers"/>, fails unless it was accepted, and returns its request and decision ids.</summary>
    public static async Task<(string Request, string Decision)> Filed(GoldenHost host, string text, (string Name, string Value)[] headers, CancellationToken ct, string kind = "rescope", string? item = null)
    {
        var answer = await File(host, Body(kind, text, item), headers, ct);
        Assert.True(answer.Status == HttpStatusCode.OK, answer.Body);
        var root = JsonDocument.Parse(answer.Body).RootElement;
        return (root.GetProperty("requestId").GetString()!, root.GetProperty("decisionId").GetString()!);
    }

    /// <summary>POST /decisions/{id} as <paramref name="headers"/>; the views are caught up before and after.</summary>
    public static async Task<Answer> Decide(GoldenHost host, string decision, string choice, string? note, (string Name, string Value)[] headers, CancellationToken ct)
    {
        await ViewScenarios.CatchUp(host, ct);
        var answer = await host.Http.Ask(Post("/sindri/decisions/" + decision, new JsonObject { ["choice"] = choice, ["note"] = note }.ToJsonString(), headers));
        await ViewScenarios.CatchUp(host, ct);
        return answer;
    }

    /// <summary>GET <paramref name="route"/> as <paramref name="headers"/> after the views caught up.</summary>
    public static async Task<string> Read(GoldenHost host, string route, (string Name, string Value)[] headers, CancellationToken ct)
    {
        await ViewScenarios.CatchUp(host, ct);
        return (await host.Http.Ask(Get(route, headers))).Body;
    }
}
