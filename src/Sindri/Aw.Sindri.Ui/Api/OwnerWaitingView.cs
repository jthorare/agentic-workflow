namespace Aw.Sindri.Ui.Api;

/// <summary>The `ownerWaiting` of a `skillWaiting` needs-you row (skill flows spec §5.4): why a skill block's session needs the owner.</summary>
/// <param name="LaunchId">The launch.</param>
/// <param name="Reason">'question', 'noResult', 'unreadable' or 'skillUpdated'.</param>
/// <param name="Text">The skill's question; null (left out on the wire) for every other reason.</param>
/// <param name="ResultPath">The result file the reader looked at.</param>
/// <param name="Detail">The parse error or the other skill's name; null (left out on the wire) otherwise.</param>
/// <param name="Instance">The workflow instance's address.</param>
/// <param name="State">The Step's state id in that instance.</param>
/// <param name="Attempt">The Step run's number, or null.</param>
internal sealed record OwnerWaitingView(string LaunchId, string Reason, string ResultPath, string Instance, string State, string? Text = null, string? Detail = null, int? Attempt = null);
