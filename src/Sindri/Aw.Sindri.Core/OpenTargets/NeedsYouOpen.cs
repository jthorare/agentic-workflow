using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.OpenTargets;

/// <summary>
/// The one place the human acts on a needs-you row (owner requirement q65): the app goes from the row to the action in one click through <see cref="Route"/>.
/// Every row carries exactly one. The members after <paramref name="Label"/> are reserved for later slices and null until one fills them. The field name 'open' is a reversible default.
/// </summary>
/// <param name="Route">The deep link the app parses ('decision/&lt;id&gt;', 'workflow/&lt;address&gt;', 'service/&lt;name&gt;', 'recipe/&lt;id&gt;', 'needs-you/&lt;rowId&gt;', 'setup').</param>
/// <param name="Label">The button's plain words ("Answer the question", "Review and decide").</param>
/// <param name="Pr">The pull request the row is about; null (omitted) until a slice fills it.</param>
/// <param name="Artifact">The artifact the row is about; null (omitted) until a slice fills it.</param>
/// <param name="Session">The agent session the row is about, '&lt;provider&gt;/&lt;id&gt;'; null (omitted) until a slice fills it.</param>
/// <param name="ReasonCode">The machine-readable reason; null (omitted) until a slice fills it.</param>
public sealed record NeedsYouOpen(
    string Route,
    string Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NeedsYouOpenPr? Pr = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NeedsYouOpenArtifact? Artifact = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Session = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReasonCode = null);
