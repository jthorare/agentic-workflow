namespace Aw.Judge.Features.ImportTurns;

/// <summary>The ImportTurns request ('judge eval import-turns &lt;transcript.jsonl&gt;... [--since 14d]').</summary>
/// <param name="Files">The transcript files.</param>
/// <param name="Since">Only files modified within this long (--since), or null for every file.</param>
public sealed record ImportTurnsRequest(IReadOnlyList<string> Files, TimeSpan? Since = null);
