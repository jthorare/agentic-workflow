using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>What was read of one transcript: the byte just past its last complete line, and each message by id with its timestamp.</summary>
internal sealed class FileState
{
    public long Offset { get; set; }

    /// <summary>True when this state changed since it was last saved to the read cache.</summary>
    public bool Dirty { get; set; }

    public Dictionary<string, (DateTimeOffset? At, ModelTokens Tokens)> Messages { get; } = new(StringComparer.Ordinal);
}
