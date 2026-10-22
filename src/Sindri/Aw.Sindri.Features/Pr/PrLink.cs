using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Pr;

/// <summary>Reads the pull request's link a babysitter Step was given: 'fact:pr' as a bare link or an object with 'url' or 'link', else the 'pr' argument.</summary>
internal static class PrLink
{
    /// <summary>The link, or null when the Step was given none.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>The link.</returns>
    public static string? Of(StepContext context)
    {
        var fact = StepFacts.Read(context, "fact:pr");
        var link = fact switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonObject obj => Text(obj["url"]) ?? Text(obj["link"]),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(link) && context.Arguments.TryGetValue("pr", out var argument) && argument.ValueKind == JsonValueKind.String)
        {
            link = argument.GetString();
        }

        return string.IsNullOrWhiteSpace(link) ? null : link.Trim();
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
