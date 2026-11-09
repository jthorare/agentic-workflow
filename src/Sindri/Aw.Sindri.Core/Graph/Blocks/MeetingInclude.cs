namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'meetings' node reads of each meeting.</summary>
public enum MeetingInclude
{
    /// <summary>The notes only.</summary>
    Notes = 1,

    /// <summary>The notes and the full transcript.</summary>
    Transcript,
}
