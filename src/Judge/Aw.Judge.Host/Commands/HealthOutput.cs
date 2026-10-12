namespace Aw.Judge.Host.Commands;

/// <summary>What 'judge health' prints: {"status": "ok|degraded|down", "failures24h": N} (hook contract).</summary>
/// <param name="Status">ok, degraded or down.</param>
/// <param name="Failures24h">The provider failures in the last 24 hours.</param>
internal sealed record HealthOutput(string Status, int Failures24h);
