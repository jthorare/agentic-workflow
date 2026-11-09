using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// The run occurrence of a polled event: a number made from its delivery id, so the same event always has the same run id ('slug@rev:occurrence') and a second start is refused (SND-GRAPH-100).
/// A run id's occurrence is a number of at most 9 digits, so the id is hashed (SHA-256, first 4 bytes) into 100,000,000 to 999,999,998. The range stays above the small numbers a manual run counts up from.
/// Two different delivery ids of one workflow revision share a number about once in 30,000 events; the second would then read as a duplicate.
/// </summary>
internal static class DeliveryOccurrence
{
    private const int Floor = 100_000_000;
    private const uint Span = 899_999_999;

    /// <summary>Turns a delivery id into its occurrence number.</summary>
    /// <param name="deliveryKey">The event's delivery id ('github:1001').</param>
    /// <returns>A number from 100,000,000 to 999,999,998.</returns>
    internal static int Of(string deliveryKey)
    {
        ArgumentNullException.ThrowIfNull(deliveryKey);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deliveryKey));
        var number = BitConverter.ToUInt32(hash, 0);
        return checked((int)(Floor + (number % Span)));
    }
}
