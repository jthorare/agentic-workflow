using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The commit a phase worktree was seeded at (sandbox spec §3.8), read from the stored 'fact:lineContext' the seed Step used. Nothing writes that fact: it is a payload input the line's starter passes
/// (recipes/sindri/workflow.line.yaml), and 'step.phase.seed' (<c lang="text">PhaseSeedStep.Line</c>, src/Sindri/Aw.Sindri.Features/Build/PhaseSeedStep.cs) seeds the worktree from the same 'base' key; this reads it the same way
/// (a full lowercase 40-character sha), and the tests tie the two together. A phase stacked on a parent's head is seeded at the parent's head instead, so this is the line's base for it.
/// </summary>
internal static class PhaseSeed
{
    /// <summary>The line's base commit: a full 40-character lowercase sha in 'base'.</summary>
    /// <param name="lineContext">The fact's JSON text.</param>
    /// <returns>The sha; null when the text is not a line context with a valid base.</returns>
    internal static string? BaseOf(string lineContext)
    {
        try
        {
            using var document = JsonDocument.Parse(lineContext);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("base", out var value) && value.ValueKind == JsonValueKind.String
                && value.GetString() is { Length: 40 } sha && sha.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
                    ? sha
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
