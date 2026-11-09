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
    public static int Of(string caller, string node, int index) => Of(caller, node, index, attempt: 0);

    /// <summary>The occurrence of the callee's run after <paramref name="attempt"/> clashes with another caller's run: the same inputs and attempt always give the same number.</summary>
    /// <param name="caller">The calling run's id.</param>
    /// <param name="node">The calling node's id.</param>
    /// <param name="index">The node's item index, 0 outside a 'for_each'.</param>
    /// <param name="attempt">0 for the first try; 1, 2, ... for each retry after the id was held by a different call.</param>
    /// <returns>A number from 100,000,000 to 999,999,999; attempt 0 gives the same number as the three-argument overload.</returns>
    public static int Of(string caller, string node, int index, int attempt)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        // why: attempt 0 hashes the old text, so ids already taken stay what they were
        var text = attempt == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{caller}|{node}|{index}")
            : string.Create(CultureInfo.InvariantCulture, $"{caller}|{node}|{index}|{attempt}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Floor + (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % Span);
    }
}
