using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>A fake's answer: the status, a JSON body, and what the request was taken for (recorded and logged).</summary>
/// <param name="Status">The HTTP status code.</param>
/// <param name="Body">The JSON body.</param>
/// <param name="Operation">What the request was (GraphQL root fields or the route), or why it was refused.</param>
/// <param name="Headers">Extra response headers (rate-limit figures), or null.</param>
internal sealed record FakeHttpReply(int Status, JsonNode Body, string Operation, IReadOnlyDictionary<string, string>? Headers = null)
{
    /// <summary>The status that makes the server drop the connection without a reply (an unknown outcome for the client).</summary>
    public const int DroppedStatus = 0;

    /// <summary>A reply that is never sent: the connection is aborted.</summary>
    /// <param name="operation">What the request was.</param>
    /// <returns>The reply.</returns>
    public static FakeHttpReply Dropped(string operation) => new(DroppedStatus, new JsonObject(), operation);
}
