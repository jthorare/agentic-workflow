namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The write key's state after a save.</summary>
/// <param name="State">resolves or scopeUnknown ("Linear doesn't say if this key can write").</param>
internal sealed record LinearWriteState(string State);
