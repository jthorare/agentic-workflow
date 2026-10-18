using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// An agent's session id (collector spec §2; launcher spec L-9's generic pattern): 1 to 128 of letters, digits, '.', '_', ':' and '-',
/// with no leading '.' and no '..'. A process whose id isn't known gets 'pid-&lt;pid&gt;-&lt;startTicks&gt;'.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SessionId>))]
public readonly partial struct SessionId : ITypedId<SessionId, string>
{
    /// <summary>The id of a process whose agent's session id can't be learned: stable for the process's life.</summary>
    /// <param name="pid">The process id.</param>
    /// <param name="startTicks">The process's start time in clock ticks.</param>
    /// <returns>The id.</returns>
    public static SessionId OfProcess(int pid, long startTicks) => From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"pid-{pid}-{startTicks}"));

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A session id is 1 to 128 of letters, digits, '.', '_', ':' or '-', not starting with '.' and with no '..'");

    [GeneratedRegex("^(?!\\.)(?!.*\\.\\.)[A-Za-z0-9._:-]{1,128}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
