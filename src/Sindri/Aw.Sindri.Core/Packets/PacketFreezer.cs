namespace Aw.Sindri.Core.Packets;

/// <summary>Who froze a packet (line spec §2, LN-PKT5): the record keeps it, and a freeze is write-once.</summary>
public enum PacketFreezer
{
    /// <summary>A Step froze it.</summary>
    Machine = 1,

    /// <summary>The owner's decision froze it.</summary>
    Owner = 2,
}
