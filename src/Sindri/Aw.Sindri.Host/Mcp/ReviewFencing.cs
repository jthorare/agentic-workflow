using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Features.GetLibrary;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The Reviews list's fencing for a session (<see cref="McpFencing"/> with <see cref="FencePlan.Reviews"/>).</summary>
/// <param name="json">The host's JSON options.</param>
internal sealed class ReviewFencing(JsonSerializerOptions json) : IReviewFencing
{
    public IResult List(ListReviewsResponse list)
    {
        using var document = JsonDocument.Parse(AwJson.Serialize(list, json));
        return TypedResults.Text(McpFencing.Apply(document.RootElement, FencePlan.Reviews).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }

    public IResult Library(GetLibraryResponse library)
    {
        // why: a folder's path and why it can't be read are the owner's machine; a session reads the docs it may, never the folders or their state
        using var document = JsonDocument.Parse(AwJson.Serialize(library with { UnreadableRoots = [], Roots = [] }, json));
        return TypedResults.Text(McpFencing.Apply(document.RootElement, FencePlan.Library).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }
}
