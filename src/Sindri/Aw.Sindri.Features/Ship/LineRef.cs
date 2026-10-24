using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Ship;

/// <summary>The line a phase belongs to, from fact:lineContext.</summary>
/// <param name="Name">The line's name.</param>
/// <param name="Phase">The phase's id.</param>
/// <param name="Attempt">The attempt, from 1.</param>
internal sealed record LineRef(string Name, string Phase, int Attempt)
{
    /// <summary>Reads the line; null for an empty context (a phase run outside a line).</summary>
    /// <param name="context">The fact.</param>
    /// <returns>The line.</returns>
    internal static LineRef? Of(JsonNode? context) =>
        StepFacts.Field(context, "line") is { } name && StepFacts.Field(context, "phase") is { } phase
            ? new LineRef(name, phase, context?["attempt"] is JsonValue a && a.TryGetValue<int>(out var attempt) ? attempt : 1)
            : null;
}
