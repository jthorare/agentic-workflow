using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Features.GetLibraryDoc;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>A design doc's fencing for a session (<see cref="McpFencing"/> with <see cref="FencePlan.LibraryDoc"/>).</summary>
/// <param name="json">The host's JSON options.</param>
internal sealed class LibraryDocFencing(JsonSerializerOptions json) : ILibraryDocFencing
{
    public IResult Doc(GetLibraryDocResponse doc)
    {
        using var document = JsonDocument.Parse(AwJson.Serialize(doc, json));
        return TypedResults.Text(McpFencing.Apply(document.RootElement, FencePlan.LibraryDoc).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }
}
