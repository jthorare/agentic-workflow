using System.Text.Json;

namespace Aw.Meta.Features.ReadEvents;

/// <summary>One event as the command prints it.</summary>
/// <param name="Id">Its number.</param>
/// <param name="At">When it happened.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Lane">The lane it concerns, or null.</param>
/// <param name="Fields">Its fields.</param>
public sealed record EventLine(long Id, DateTimeOffset At, string Kind, string? Lane, JsonElement Fields);
