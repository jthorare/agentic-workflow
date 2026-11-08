using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// A connection's name: '&lt;service&gt;:&lt;label&gt;' ('linear:work'), both parts lowercase letters, digits and '-' (1 to 32 characters, starting with a letter or digit).
/// </summary>
public readonly record struct ConnectionName
{
    /// <summary>Gets the service part ('linear').</summary>
    public string Service { get; init; }

    /// <summary>Gets the label part ('work').</summary>
    public string Label { get; init; }

    /// <summary>Parses a name.</summary>
    /// <param name="text">The name as typed.</param>
    /// <returns>The name, or SND-CONN-008. The error does not repeat text that is not a name part.</returns>
    public static Result<ConnectionName> Parse(string text) => throw new NotSupportedException();

    /// <summary>The name as typed back: '&lt;service&gt;:&lt;label&gt;'.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => string.Concat(Service, ":", Label);
}
