using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>A fake's answer: the status, a JSON body, and what the request was taken for (recorded and logged).</summary>
/// <param name="Status">The HTTP status code.</param>
/// <param name="Body">The JSON body.</param>
/// <param name="Operation">What the request was (GraphQL root fields or the route), or why it was refused.</param>
internal sealed record FakeHttpReply(int Status, JsonNode Body, string Operation);
