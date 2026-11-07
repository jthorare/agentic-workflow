using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A comment that mentions the viewer, derived when it is read (Linear hub spec 2026-11-04 §4.3).</summary>
/// <param name="Issue">The issue's record key.</param>
/// <param name="Identifier">The issue's identifier, 'ENG-12'.</param>
/// <param name="Comment">The comment.</param>
public sealed record LinearSinceMention(RecordKey Issue, string Identifier, LinearCommentRow Comment);
