namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>PUT /sindri/reviews/settings's answer.</summary>
/// <param name="Approved">True when the change took effect; false while it waits for a profile approval.</param>
/// <param name="PendingCount">How many profile changes wait for approval.</param>
internal sealed record ReviewSettingsSaved(bool Approved, int PendingCount);
