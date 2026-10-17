using System.Security.Cryptography;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IRandomHex over the operating system's cryptographic random source.</summary>
internal sealed class OsRandomHex : IRandomHex
{
    /// <inheritdoc />
    public string Hex(int bytes) => RandomNumberGenerator.GetHexString(bytes * 2, lowercase: true);
}
