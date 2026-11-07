namespace Aw.Sindri.Core.Linear;

/// <summary>The last stored check of each Linear key; null for a key never saved through Settings (or a write key since removed).</summary>
/// <param name="Read">The read key's check.</param>
/// <param name="Write">The write key's check.</param>
public sealed record LinearStoredChecks(LinearStoredCheck? Read, LinearStoredCheck? Write);
