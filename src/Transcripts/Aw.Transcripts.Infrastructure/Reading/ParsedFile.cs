using Aw.Transcripts.Core.Turns;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>What one pass over a transcript file found. A provider reader fills it line by line.</summary>
internal sealed class ParsedFile
{
    public List<TranscriptEntry> Entries { get; } = [];

    /// <summary>The first and last timestamps any line carried.</summary>
    public DateTimeOffset? First { get; private set; }

    public DateTimeOffset? Last { get; private set; }

    /// <summary>The first working directory a line named.</summary>
    public string? Cwd { get; set; }

    /// <summary>Set by a reader whose file says so itself (a Codex rollout's session_meta).</summary>
    public bool IsSubagent { get; set; }

    /// <summary>A Codex subagent's parent thread id.</summary>
    public string? ParentThread { get; set; }

    /// <summary>A Codex 'exec' run: its prompt came on the command line, typed by a script as often as a person.</summary>
    public bool NonInteractive { get; set; }

    public int SkippedLines { get; set; }

    public void Saw(DateTimeOffset at)
    {
        First = First is { } first && first <= at ? first : at;
        Last = Last is { } last && last >= at ? last : at;
    }
}
