using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands.Pins;

/// <summary>One pin of a <see cref="PinList"/>.</summary>
/// <param name="Id">The pin's id, or null for an entry that gets its id from its name.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Aliases">The words people use for it.</param>
/// <param name="Features">The exact feature keys (or globs) it names.</param>
/// <param name="SetIn">'profile' or 'repo &lt;name&gt;': where the entry lives.</param>
internal sealed record PinListEntry(string? Id, string Name, IReadOnlyList<string> Aliases, IReadOnlyList<string> Features, string SetIn)
{
    internal static PinListEntry From(ConceptSettings pin, string setIn)
    {
        ArgumentNullException.ThrowIfNull(pin);
        return new PinListEntry(pin.Id?.Value, pin.Name ?? string.Empty, pin.Aliases ?? [], pin.Features ?? [], setIn);
    }
}
