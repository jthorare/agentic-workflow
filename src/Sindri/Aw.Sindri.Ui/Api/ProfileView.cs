namespace Aw.Sindri.Ui.Api;

/// <summary>The profile part of the status.</summary>
/// <param name="Approved">The approved hash the control plane runs with, if any.</param>
/// <param name="Live">The live file's hash, when it loads.</param>
/// <param name="ChangedSinceApproval">True when the live file differs from the approval.</param>
/// <param name="Problem">Why the live file doesn't load, if it doesn't.</param>
/// <param name="Source">The live profile file, when it loads.</param>
/// <param name="NeedsApproval">Sindri's verdict that the profile needs the owner's approval (the app never derives it).</param>
/// <param name="ModelPick">Sindri's verdict on model choice: 'on' or 'off' (the app never derives it); 'off' from a Sindri that doesn't serve it.</param>
/// <param name="LabelPick">Sindri's verdict on the Jev label pick: 'off', 'on' or 'needs-key' (the app never derives it); 'off' from a Sindri that doesn't serve it.</param>
internal sealed record ProfileView(string? Approved, string? Live, bool ChangedSinceApproval, string? Problem, string? Source, bool NeedsApproval, string ModelPick = "off", string LabelPick = "off");
