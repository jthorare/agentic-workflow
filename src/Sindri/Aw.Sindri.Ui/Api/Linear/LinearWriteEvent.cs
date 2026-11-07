namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>
/// The data of a <c language="text">linear.write.requested|applied|failed|refused</c> event (Linear hub 1b contract §5c). Never carries typed text. A sheet open on
/// <see cref="Key"/> re-reads the issue on <c language="text">applied</c>; a sync tick is <c language="text">context.sources.synced</c> with source "linear" and carries no keys.
/// </summary>
/// <param name="Key">The issue key; null on a create's requested and failed.</param>
/// <param name="Team">The team key.</param>
/// <param name="ClientId">The app's client id; null for the writeback service.</param>
/// <param name="Attempt">The attempt number.</param>
/// <param name="Write">edit, comment or create.</param>
/// <param name="Outcome">applied, failed or refused.</param>
/// <param name="Code">The SND code of a failed or refused write; else null.</param>
internal sealed record LinearWriteEvent(string? Key, string Team, string? ClientId, int? Attempt, string Write, string Outcome, string? Code);
