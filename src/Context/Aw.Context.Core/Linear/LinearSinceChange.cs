using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>An assignee or state change the strip shows (Linear hub spec 2026-11-04 §4.3).</summary>
/// <param name="Issue">The issue's record key.</param>
/// <param name="Identifier">The issue's identifier, 'ENG-12'.</param>
/// <param name="Field">'assignee' or 'state'.</param>
/// <param name="Old">The id before (a user id or a state id), or null.</param>
/// <param name="New">The id after, or null.</param>
/// <param name="At">When Sindri saw the change.</param>
/// <param name="OldName">The state's name before, as the sync recorded it with the change; null for an assignee change or a change recorded before names were kept.</param>
/// <param name="NewName">The state's name after, recorded the same way.</param>
/// <param name="Via">'feed' (a sync saw it) or 'write-through' (the owner's own save).</param>
public sealed record LinearSinceChange(RecordKey Issue, string Identifier, string Field, ExternalId? Old, ExternalId? New, DateTimeOffset At, string Via, string? OldName = null, string? NewName = null);
