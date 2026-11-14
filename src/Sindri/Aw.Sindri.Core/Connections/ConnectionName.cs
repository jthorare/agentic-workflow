using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// A connection's name: '&lt;service&gt;:&lt;label&gt;' ('linear:work'), both parts lowercase letters, digits and '-' (1 to 32 characters, starting with a letter or digit).
/// </summary>
public readonly partial record struct ConnectionName
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Part { get; }

    /// <summary>Gets the service part ('linear').</summary>
    public string Service { get; init; }

    /// <summary>Gets the label part ('work').</summary>
    public string Label { get; init; }

    /// <summary>Parses a name.</summary>
    /// <param name="text">The name as typed.</param>
    /// <returns>The name, or SND-CONN-008. The error does not repeat text that is not a name part.</returns>
    public static Result<ConnectionName> Parse(string text)
    {
        var pieces = (text ?? string.Empty).Split(':');
        if (pieces.Length != 2 || !Part.IsMatch(pieces[0]) || !Part.IsMatch(pieces[1]))
        {
            return SindriErrors.ConnectionNameInvalid.With("a connection name is <service>:<label>, lowercase letters, digits and '-', 1 to 32 characters each");
        }

        return new ConnectionName { Service = pieces[0], Label = pieces[1] };
    }

    /// <summary>
    /// Parses a name that the generic add or a rename asks for. slack:messages has no preset on purpose and only sindri messages connect makes it (#3279, #3640),
    /// so neither may create it, and a rename must not move it away.
    /// </summary>
    /// <param name="text">The name as typed, 'service:label'.</param>
    /// <returns>The name, SND-CONN-017 for slack:messages, or SND-CONN-008.</returns>
    public static Result<ConnectionName> ParseUnreserved(string text) =>
        string.Equals(text, "slack:messages", StringComparison.Ordinal)
            ? SindriErrors.ConnectionNameReserved.With("slack:messages is made by sindri messages connect (fix: run sindri messages connect)")
            : Parse(text);

    /// <summary>The name as typed back: '&lt;service&gt;:&lt;label&gt;'.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => string.Concat(Service, ":", Label);
}
