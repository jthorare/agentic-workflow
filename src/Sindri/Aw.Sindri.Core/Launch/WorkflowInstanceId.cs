using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// An instance's opaque id (spec §5.1, §6.3): 'WFI-' and 32 lower-case hex digits. The CLI's '/'-separated
/// addresses are <see cref="InstanceAddress"/>; only this id crosses the API. Every instance's id is minted from its
/// address (<see cref="Of"/>), so a root and a child get theirs the same way and the same instance always has the same id.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<WorkflowInstanceId>))]
public readonly partial struct WorkflowInstanceId : ITypedId<WorkflowInstanceId, string>
{
    /// <summary>The id for <paramref name="value"/>, written as 'WFI-' and its 32 hex digits.</summary>
    /// <param name="value">A fresh id from the id generator, or one derived from a trigger key.</param>
    /// <returns>The id.</returns>
    public static WorkflowInstanceId For(Guid value) => From("WFI-" + value.ToString("N"));

    /// <summary>
    /// The id of the instance at <paramref name="address"/> (spec §5.1, §6.3): a UUIDv8 over the SHA-256 of the address,
    /// which is its workflow id.
    /// </summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>Its id.</returns>
    public static WorkflowInstanceId Of(InstanceAddress address)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(Encoding.UTF8.GetBytes(address.Value), digest);
        var bytes = digest[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return For(new Guid(bytes, bigEndian: true));
    }

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An instance id is 'WFI-' and 32 lower-case hex digits");

    [GeneratedRegex("^WFI-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
