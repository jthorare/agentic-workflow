using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The session reads' fencing for a session caller (<see cref="McpFencing"/> with <see cref="FencePlan.Sessions"/> or <see cref="FencePlan.OneSession"/>).</summary>
/// <param name="json">The host's JSON options.</param>
internal sealed class SessionFencing(JsonSerializerOptions json) : ISessionFencing
{
    public IResult List(SessionList list) => Fenced(AwJson.Serialize(list, json), FencePlan.Sessions);

    public IResult One(SessionView view) => Fenced(AwJson.Serialize(view, json), FencePlan.OneSession);

    private static Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult Fenced(byte[] body, FencePlan plan)
    {
        using var document = JsonDocument.Parse(body);
        return TypedResults.Text(McpFencing.Apply(document.RootElement, plan).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }
}
