using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Graph.Guards;

/// <summary>The occurrence number of a run that another workflow started: made from the caller run, node and index, so calling twice gives one run.</summary>
public static class CallOccurrence
{
    private const int Floor = 100_000_000;
    private const uint Span = 900_000_000;

    /// <summary>The occurrence of the callee's run.</summary>
    /// <param name="caller">The calling run's id.</param>
    /// <param name="node">The calling node's id.</param>
    /// <param name="index">The node's item index, 0 outside a 'for_each'.</param>
    /// <returns>A number from 100,000,000 to 999,999,999: above the numbers a trigger counts up to, so a call never takes a manual run's id.</returns>
    public static int Of(string caller, string node, int index)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(node);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{caller}|{node}|{index}")));
        return Floor + (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % Span);
    }
}
