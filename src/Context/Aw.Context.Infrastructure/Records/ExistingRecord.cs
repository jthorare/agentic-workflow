namespace Aw.Context.Infrastructure.Records;

/// <summary>What a page's record is compared with before it is written.</summary>
/// <param name="Version">The stored version.</param>
/// <param name="TextHash">The stored text hash.</param>
/// <param name="Ruleset">The stored scrub ruleset.</param>
/// <param name="Blocked">Whether it was withheld.</param>
/// <param name="Removed">Whether it is a tombstone.</param>
internal sealed record ExistingRecord(string Version, string TextHash, string Ruleset, bool Blocked, bool Removed);
