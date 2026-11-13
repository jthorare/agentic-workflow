using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The live profile's state (#2258 PR 1: edits apply on save, so there is no approval state).</summary>
/// <param name="Live">The live file's hash, when it loads.</param>
/// <param name="Problem">Why the live profile file doesn't load, if it doesn't.</param>
/// <param name="Source">The live profile file, when it loads.</param>
/// <param name="ModelPick">'on' when the profile has model choice on (workflow.agent.pick), else 'off': computed here so the app never derives it (model-pick spec 8).</param>
/// <param name="LabelPick">'off', 'on' or 'needs-key' for the Jev label pick (context.models.labeling.pick); 'needs-key' when it is on and Jev has no key. Computed here so the app never derives it (model routing spec §8.5).</param>
/// <param name="SurfacePick">'off', 'on' or 'needs-key' for the Jev product-surface pick (context.models.productSurfaces.pick), computed the way <paramref name="LabelPick"/> is (model routing spec §8.5).</param>
/// <param name="ProblemKind">Why <paramref name="Problem"/> is set: 'missing' (no profile file yet) or 'invalid' (the file is there and won't load); null when the profile loads. Lets the app tell a new home from a broken file without matching on text (#3113).</param>
public sealed record ProfileStatus(ProfileHash? Live, string? Problem, string? Source, string ModelPick = "off", string LabelPick = "off", string SurfacePick = "off", string? ProblemKind = null);
