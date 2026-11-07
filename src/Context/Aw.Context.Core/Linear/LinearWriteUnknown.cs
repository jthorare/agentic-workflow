namespace Aw.Context.Core.Linear;

/// <summary>Sent, and no answer came (SND-LINEAR-011), and the one re-read afterwards didn't find the write either. Create and Comment are found by their client id, Change by the sent fields' values (all at the new value counts as applied), Attach by its url. The caller may retry with the same client id.</summary>
public sealed record LinearWriteUnknown : LinearWriteOutcome;
