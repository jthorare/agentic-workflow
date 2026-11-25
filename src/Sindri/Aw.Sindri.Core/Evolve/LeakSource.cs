namespace Aw.Sindri.Core.Evolve;

/// <summary>The turns of one session that the model saw for a proposal, recorded by code (never read from the model's answer).</summary>
/// <param name="Session">The opaque session ref.</param>
/// <param name="Turns">The scrubbed turn texts, in order.</param>
public sealed record LeakSource(string Session, IReadOnlyList<string> Turns);
