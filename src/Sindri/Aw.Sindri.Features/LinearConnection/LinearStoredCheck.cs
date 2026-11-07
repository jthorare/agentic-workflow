namespace Aw.Sindri.Features.LinearConnection;

/// <summary>What the last save found out about one Linear key, kept so a read of the connection needs no key lookup.</summary>
/// <param name="Pointer">The profile pointer the key was saved under; a read trusts the check only while the profile still holds this pointer.</param>
/// <param name="State">'resolves', or for the write key also 'scopeUnknown'.</param>
/// <param name="At">When the save checked it.</param>
internal sealed record LinearStoredCheck(string Pointer, string State, DateTimeOffset At);
