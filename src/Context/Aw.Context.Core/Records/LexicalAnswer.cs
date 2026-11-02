namespace Aw.Context.Core.Records;

/// <summary>What the lexical leg answered.</summary>
/// <param name="Hits">The best chunk of each matching record, best first; empty when nothing matched or the leg is degraded.</param>
/// <param name="Degraded">Set when the store could not answer (empty or not yet synced), else null. A scope that lets nothing through is not degraded: it answers like no match.</param>
public sealed record LexicalAnswer(IReadOnlyList<RecordHit> Hits, LexicalDegraded? Degraded);
