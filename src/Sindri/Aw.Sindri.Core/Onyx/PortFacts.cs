namespace Aw.Sindri.Core.Onyx;

/// <summary>Which loopback ports Onyx's web port could use.</summary>
/// <param name="Preferred">The port Onyx wants.</param>
/// <param name="PreferredFree">Nothing answers on it on 127.0.0.1 or [::1] and 127.0.0.1 can bind it.</param>
/// <param name="FirstFreeFallback">The first free port of the fallback range, or null when every one is taken or when the preferred port is free (the fallbacks are probed only when it is taken).</param>
public sealed record PortFacts(int Preferred, bool PreferredFree, int? FirstFreeFallback);
