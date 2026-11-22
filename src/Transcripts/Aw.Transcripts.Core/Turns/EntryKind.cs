namespace Aw.Transcripts.Core.Turns;

/// <summary>What one transcript line is, as a provider reader decides it.</summary>
public enum EntryKind
{
    /// <summary>A prompt a person typed.</summary>
    Human,

    /// <summary>The agent's text or tool calls.</summary>
    Assistant,

    /// <summary>A prompt-like message no person typed: a teammate or peer message, a task notification, a subagent's task, a compaction summary. It ends the assistant turn before it and is not a turn itself.</summary>
    MachinePrompt,
}
