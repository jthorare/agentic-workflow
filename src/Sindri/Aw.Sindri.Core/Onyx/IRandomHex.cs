namespace Aw.Sindri.Core.Onyx;

/// <summary>Random bytes as hex, for the key material Onyx's .env needs.</summary>
public interface IRandomHex
{
    /// <summary>Makes random bytes from the operating system's random source.</summary>
    /// <param name="bytes">How many bytes.</param>
    /// <returns>Twice as many lowercase hex digits.</returns>
    public string Hex(int bytes);
}
