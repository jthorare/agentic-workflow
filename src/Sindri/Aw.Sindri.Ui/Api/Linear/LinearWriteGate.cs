namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The <c language="text">write</c> member of GET /sindri/linear/issues/{key} (Linear hub 1b contract §0): whether the sheet's fields can be edited.</summary>
/// <param name="Allowed">True when Sindri can write to this issue.</param>
/// <param name="Code">Why not: SND-LINEAR-002 (no write key), SND-LINEAR-003 (the issue's team isn't picked), SND-AUTH-002 (a session read); null when allowed, or when the issue is removed.</param>
internal sealed record LinearWriteGate(bool Allowed, string? Code);
