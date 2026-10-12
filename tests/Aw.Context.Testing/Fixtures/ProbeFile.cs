namespace Aw.Context.Testing.Fixtures;

/// <summary>The shape of 'probes.json'.</summary>
/// <param name="Probes">The probes, in file order.</param>
public sealed record ProbeFile(IReadOnlyList<Probe> Probes);
