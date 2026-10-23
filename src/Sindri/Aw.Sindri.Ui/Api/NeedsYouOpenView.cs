namespace Aw.Sindri.Ui.Api;

/// <summary>The 'open' target of a needs-you row (q65): the one place the owner acts on it.</summary>
/// <param name="Route">The deep link ("decision/DEC-…", "workflow/&lt;address&gt;", "service/&lt;name&gt;", "recipe/&lt;id&gt;", "needs-you/&lt;rowId&gt;", "setup").</param>
/// <param name="Label">The button's plain words.</param>
/// <param name="Pr">The pull request the row is about; null (omitted) until the API serves one.</param>
/// <param name="Artifact">The artifact the row is about; null (omitted) until the API serves one.</param>
/// <param name="Session">The agent session ("provider/id"); null (omitted) until the API serves one.</param>
/// <param name="ReasonCode">The machine-readable reason; null (omitted) until the API serves one.</param>
internal sealed record NeedsYouOpenView(
    string? Route = null,
    string? Label = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NeedsYouOpenPrView? Pr = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NeedsYouOpenArtifactView? Artifact = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Session = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ReasonCode = null);
