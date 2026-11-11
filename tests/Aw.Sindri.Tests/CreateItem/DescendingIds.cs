using Aw.Kernel.Ids;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>Ids that sort in the order opposite to the order they are made, as two ids made in one millisecond can.</summary>
internal sealed class DescendingIds : IIdGenerator
{
    private long _next = long.MaxValue;

    public Guid NewId()
    {
        var bytes = new byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(8), _next--);
        return new Guid(bytes, bigEndian: true);
    }
}
