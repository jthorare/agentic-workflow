namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The Needs you row of an unknown write outcome (Linear hub 1b contract §5d): kind <c language="text">linear.write.unknown</c>, a chip to sindri://linear/KEY when Key is set.</summary>
/// <param name="Kind">"linear.write.unknown".</param>
/// <param name="Key">The issue key; null for a create.</param>
/// <param name="Team">The team key.</param>
/// <param name="ClientId">The write's client id.</param>
/// <param name="Write">edit, comment or create.</param>
/// <param name="At">When the write was tried.</param>
/// <param name="Text">The row's line, as the API wrote it.</param>
internal sealed record LinearWriteUnknownRow(string Kind, string? Key, string Team, string ClientId, string Write, DateTimeOffset At, string Text);
