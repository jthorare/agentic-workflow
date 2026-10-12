namespace Aw.Judge.Core.Health;

/// <summary>The judge's health, printed by 'judge health' as 'ok', 'degraded' or 'down'.</summary>
public enum HealthStatus
{
    /// <summary>No provider failed in the last 24 hours.</summary>
    Ok = 1,

    /// <summary>Some provider failed in the last 24 hours, but not every recent attempt.</summary>
    Degraded = 2,

    /// <summary>Providers failed in the last 24 hours and the last five model attempts all failed.</summary>
    Down = 3,
}
