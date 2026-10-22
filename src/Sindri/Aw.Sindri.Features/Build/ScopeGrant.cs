using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// What the owner allowed at the scope desk (line spec §7.2): 'fact:answers' is an object, <c language="text">{ allow?: string[], reason?: string, revert?: string[] }</c>, the shape the shipped
/// workflow.build evals use. 'allow' names the granted paths (repo-relative globs or 'new: &lt;path&gt;'); a grant with no path is no grant. The reason is the owner's, or a stock line when the desk gave none.
/// </summary>
/// <param name="Paths">The granted paths.</param>
/// <param name="Reason">Why the owner allowed them.</param>
internal sealed record ScopeGrant(IReadOnlyList<string> Paths, string Reason)
{
    internal const string DefaultReason = "allowed at the scope desk";

    internal static ScopeGrant? From(JsonNode? answers)
    {
        var paths = answers is JsonObject obj && obj["allow"] is JsonArray listed
            ? listed.Select(static p => p is JsonValue v && v.TryGetValue<string>(out var text) ? text : null).OfType<string>().Where(static p => !string.IsNullOrWhiteSpace(p)).ToList()
            : [];
        return paths.Count > 0 ? new ScopeGrant(paths, StepFacts.Field(answers, "reason") ?? DefaultReason) : null;
    }
}
