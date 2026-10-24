using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Ports;

/// <summary>The trait-check log ('judge/trait-check/log.jsonl'): one JSON line per assessment, which tunes the hook's thresholds.</summary>
public interface ITraitCheckLog
{
    /// <summary>Appends one entry (the time is added). Best effort: a log that can't be written is dropped, never an error.</summary>
    /// <param name="entry">The entry; it holds no source text.</param>
    public void Append(JsonObject entry);
}
