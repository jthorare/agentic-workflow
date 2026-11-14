namespace Aw.Sindri.Ui.Api;

/// <summary>The profile part of the status.</summary>
/// <param name="Live">The live file's hash, when it loads.</param>
/// <param name="Problem">Why the live file doesn't load, if it doesn't.</param>
/// <param name="Source">The live profile file, when it loads.</param>
/// <param name="ModelPick">Sindri's verdict on model choice: 'on' or 'off' (the app never derives it); 'off' from a Sindri that doesn't serve it.</param>
/// <param name="LabelPick">Sindri's verdict on the Jev label pick: 'off', 'on' or 'needs-key' (the app never derives it); 'off' from a Sindri that doesn't serve it.</param>
/// <param name="SurfacePick">Sindri's verdict on the Jev product-surface pick: 'off', 'on' or 'needs-key'; 'off' from a Sindri that doesn't serve it.</param>
/// <param name="ProblemKind">Why the live file doesn't load: 'missing' (no profile yet) or 'invalid'; null from a Sindri that doesn't serve it, and when the profile loads.</param>
internal sealed record ProfileView(string? Live, string? Problem, string? Source, string ModelPick = "off", string LabelPick = "off", string SurfacePick = "off", string? ProblemKind = null);
