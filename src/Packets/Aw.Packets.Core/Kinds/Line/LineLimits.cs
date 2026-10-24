namespace Aw.Packets.Core.Kinds.Line;

/// <summary>Bounds a line packet reader keeps, so a packet directory can't exhaust memory.</summary>
public static class LineLimits
{
    /// <summary>The most files a packet holds under its linked directories together; a bigger one is AW-PACKETS-003.</summary>
    public const int MaxLinkedFiles = 20_000;
}
