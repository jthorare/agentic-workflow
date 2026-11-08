namespace Aw.Sindri.Ui.Api;

/// <summary>A conflict or hold pick, or its Undo, that Sindri accepted (todo spec §4.4). Both fields are optional: the app reads only that the call worked.</summary>
/// <param name="Accepted">The command id and the ledger sequence the app sends as X-Sindri-After next, when the answer carries them.</param>
/// <param name="UndoUntil">When the pick's Undo window closes, when the answer carries it.</param>
internal sealed record TodoPickAcceptedView(CommandAcceptedView? Accepted = null, DateTimeOffset? UndoUntil = null);
