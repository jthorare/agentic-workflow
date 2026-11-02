namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>One launch's denial counter.</summary>
/// <param name="LaunchId">The launch.</param>
/// <param name="UpdatedAt">When the counter was last written.</param>
/// <param name="Entries">One entry per (kind, target, rule); the true counts, at most 200 distinct targets.</param>
/// <param name="Overflow">The denials past the 200-target limit, one row per kind that overflowed.</param>
/// <param name="Repo">The launch's repo (what Allow edits); null (omitted) from a server before the field.</param>
internal sealed record DenialLaunchView(
    string LaunchId,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DenialEntryView> Entries,
    IReadOnlyList<DenialOverflowView> Overflow,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Repo = null);
