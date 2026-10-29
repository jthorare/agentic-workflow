namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>A launch waiting for a free session slot, SND-LAUNCH-020 (wire contract item 6); gone once it starts.</summary>
/// <param name="Running">Sessions running now.</param>
/// <param name="Cap">The cap.</param>
internal sealed record WaitingForSlotView(int Running, int Cap);
