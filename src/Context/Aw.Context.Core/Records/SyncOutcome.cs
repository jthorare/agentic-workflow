namespace Aw.Context.Core.Records;

/// <summary>How a source's last tick ended (the 'source_cursor' row's 'last_sync' and 'last_error').</summary>
/// <param name="At">When the tick ended, whether it worked or not.</param>
/// <param name="Code">The tick's error code, or null when it worked.</param>
public sealed record SyncOutcome(DateTimeOffset At, string? Code);
