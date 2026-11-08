namespace Aw.Sindri.Core.Memory;

/// <summary>What a write did in the store.</summary>
/// <param name="Key">The fact's key; the existing fact's when a near duplicate was refreshed.</param>
/// <param name="Refreshed">True when a near duplicate with the same tags was refreshed instead of a row added.</param>
public sealed record FactWritten(string Key, bool Refreshed);
