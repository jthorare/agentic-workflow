using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>What was read of one transcript: the byte just past its last complete line, and each message by id with its timestamp.</summary>
internal sealed class FileState
{
    public long Offset { get; set; }

    /// <summary>True when this state changed since it was last saved to the read cache.</summary>
    public bool Dirty { get; set; }

    /// <summary>True when the saved rows of this transcript can't be added to and must be written again whole (the file was read again from byte 0).</summary>
    public bool Rewrite { get; set; }

    /// <summary>The ids of the messages added or changed since this state was last saved; the next save writes only these.</summary>
    public HashSet<string> ChangedIds { get; } = new(StringComparer.Ordinal);

    /// <summary>Marks everything as saved.</summary>
    public void MarkSaved()
    {
        Dirty = false;
        Rewrite = false;
        ChangedIds.Clear();
    }

    /// <summary>Marks everything as unsaved, so the next save writes this transcript whole.</summary>
    public void MarkUnsaved()
    {
        Dirty = true;
        Rewrite = true;
    }

    public Dictionary<string, (DateTimeOffset? At, ModelTokens Tokens)> Messages { get; } = new(StringComparer.Ordinal);
}
