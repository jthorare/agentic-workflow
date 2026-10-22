using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The agent run the app draws, for the unit lane. The status is engine P8's golden, tests/Aw.Sindri.Tests/Golden/Api/workflow-status/agent-run.json (the one
/// the API tests pin, so the app and the engine cannot drift); the live-session item is the API's Golden/Api/items-id/live-session.json.
/// </summary>
internal static class AgentRunFixtures
{
    /// <summary>The launch id the status names.</summary>
    internal const string Launch = "LCH-01JM3A4B5C6D7E8F";

    /// <summary>The address the status names.</summary>
    internal const string Address = "ITM-01JM3/step/1";

    /// <summary>The launch id OpenItem's requested golden answers with.</summary>
    internal const string Requested = "LCH-00000000000000000000000000000016";

    /// <summary>The status of an instance an agent session runs.</summary>
    /// <returns>The status as P8 serves it.</returns>
    internal static StatusFields Status() => Golden<StatusFields>("workflow-status", "agent-run");

    /// <summary>The same status with no run, as a server before P8 serves it.</summary>
    /// <returns>The status without the run.</returns>
    internal static StatusFields StatusWithoutRun() => Status() with { Run = null };

    /// <summary>An API golden read through the app's records.</summary>
    /// <typeparam name="T">The record.</typeparam>
    /// <param name="slug">The route slug.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The record.</returns>
    internal static T Golden<T>(string slug, string scenario)
        where T : class => Read<T>(GoldenFiles.Api(slug, scenario));

    /// <summary>The running item's body with its launch block edited.</summary>
    /// <param name="launch">Edits the launch block.</param>
    /// <returns>The body as served.</returns>
    internal static string ItemBody(Action<JsonObject> launch)
    {
        var body = GoldenFiles.Node("items-id", "running");
        launch(body["launch"]!.AsObject());
        return body.ToJsonString();
    }

    /// <summary>The running item with its launch block edited.</summary>
    /// <param name="launch">Edits the launch block.</param>
    /// <returns>The item.</returns>
    internal static ItemView Item(Action<JsonObject> launch) => Read<ItemView>(ItemBody(launch));

    /// <summary>The item with a live session to attach to: the API's items-id/live-session golden.</summary>
    /// <returns>The body as served.</returns>
    internal static string LiveSessionBody() => GoldenFiles.Api("items-id", "live-session");

    /// <summary>The item with a live session to attach to.</summary>
    /// <returns>The item.</returns>
    internal static ItemView LiveSession() => Golden<ItemView>("items-id", "live-session");

    /// <summary>Events as the instance events route serves them.</summary>
    /// <param name="rows">The events, as JSON objects of a transition.</param>
    /// <returns>The body.</returns>
    internal static string Events(params string[] rows) => $$"""{"events":[{{string.Join(',', rows)}}],"cursor":9}""";

    /// <summary>One event of a transition.</summary>
    /// <param name="sequence">The ledger sequence.</param>
    /// <param name="transition">The transition object as JSON.</param>
    /// <returns>The event as JSON.</returns>
    internal static string Event(int sequence, string transition) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"sequence":{{sequence}},"type":"workflow.transition","occurredAt":"2026-10-12T12:20:00.0000000Z","actor":"engine","transition":{{transition}}}""");

    private static T Read<T>(string body)
        where T : class => AwJson.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(body), SindriApiClient.Json).Value;
}
