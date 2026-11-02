namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>What the box has, read by the caller (this layer does no /proc reads): RAM in bytes and logical cores.</summary>
/// <param name="RamBytes">Total RAM.</param>
/// <param name="Cores">Logical cores.</param>
internal sealed record BoxFacts(long RamBytes, int Cores);
