namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>The health Sindri derives from a project's open PRs, beside Linear's.</summary>
/// <param name="Level">"onTrack", "atRisk", "offTrack" or "unknown"; a level this build doesn't know is kept as served.</param>
/// <param name="Reason">The counts in words ("At risk: 3 of 9 PRs red").</param>
internal sealed record ProjectTruthHealthView(string Level, string Reason);
