namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One risky path of a settings entry (spec §7.1): the paths and the reason shown beside them.</summary>
/// <param name="Globs">The paths.</param>
/// <param name="Why">Why they are risky.</param>
internal sealed record ReviewSettingsRisk(IReadOnlyList<string> Globs, string Why);
