namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>GET /sindri/sandbox/check (Check again, read-only; wire contract item 4): every blocker, and the Steps a resume would start.</summary>
/// <param name="Blockers">Every blocker in the owner's words.</param>
/// <param name="Ready">True when there is none.</param>
/// <param name="Resume">The parked Steps a resume would start; null (omitted) from a server before the field.</param>
/// <param name="NotChecked">Checks that did not run, in the owner's words (informational: not a blocker); empty from a server before the field.</param>
/// <param name="CheckedAt">When this check ran; null (omitted) from a server before the field.</param>
internal sealed record SandboxCheckView(
    IReadOnlyList<SandboxBlockerLineView> Blockers,
    bool Ready,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SandboxResumeRowView>? Resume = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? CheckedAt = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? NotChecked = null);
