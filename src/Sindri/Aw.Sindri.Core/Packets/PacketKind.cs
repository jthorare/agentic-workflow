namespace Aw.Sindri.Core.Packets;

/// <summary>The kind of packet aw-packets checks, hashes and freezes (docs/csharp/packets.md): the build packet, the scope packet and the line's six.</summary>
public enum PacketKind
{
    /// <summary>The build packet (aw-packets' default kind).</summary>
    Build = 1,

    /// <summary>The scope packet.</summary>
    Scoping = 2,

    /// <summary>A design packet.</summary>
    Design = 3,

    /// <summary>A plan packet.</summary>
    Plan = 4,

    /// <summary>A phase packet.</summary>
    Phase = 5,

    /// <summary>A proof packet.</summary>
    Proof = 6,

    /// <summary>A review packet.</summary>
    Review = 7,

    /// <summary>A handoff packet.</summary>
    Handoff = 8,
}
