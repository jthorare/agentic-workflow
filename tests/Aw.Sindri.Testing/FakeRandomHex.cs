using System.Globalization;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>A random source that makes a different value each call, from a counter: the first call is 00…01.</summary>
public sealed class FakeRandomHex : IRandomHex
{
    /// <summary>Gets the number of calls.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public string Hex(int bytes)
    {
        Calls++;
        return Calls.ToString("x", CultureInfo.InvariantCulture).PadLeft(bytes * 2, '0');
    }
}
