using System.Text.Json;
using Aw.Kernel.Json;
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
}
