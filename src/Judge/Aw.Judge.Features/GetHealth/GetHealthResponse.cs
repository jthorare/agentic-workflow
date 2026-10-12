using Aw.Judge.Core.Health;

namespace Aw.Judge.Features.GetHealth;

/// <summary>The GetHealth response.</summary>
/// <param name="Status">The judge's health.</param>
/// <param name="Failures24h">The provider failures in the last 24 hours.</param>
public sealed record GetHealthResponse(HealthStatus Status, int Failures24h);
