using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's top-level 'review:' block (PR review page spec §7). Not 'workflow.review', which holds the review lens models. Left out of the canonical JSON
/// when absent, so a profile without it keeps the hash it was approved under.
/// </summary>
/// <param name="Login">The owner's GitHub login: which PRs are "yours" and "waiting on you".</param>
/// <param name="Credential">A secret pointer for the owner's own posting token: 'env:NAME' or 'command:&lt;argv&gt;', never a value (same forms as 'workflow.forge.credential').</param>
/// <param name="Settings">Named settings; each may extend 'default' or another entry.</param>
/// <param name="Projects">Which repos belong to which project, and the settings their PRs get.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrReviewSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Login = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Credential = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, PrReviewSettingsEntry>? Settings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PrReviewProject>? Projects = null);
