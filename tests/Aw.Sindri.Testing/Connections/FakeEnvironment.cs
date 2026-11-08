using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IEnvironmentReader over a dictionary.</summary>
public sealed class FakeEnvironment : IEnvironmentReader
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    /// <summary>Sets a variable.</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, string value) => _variables[name] = value;

    /// <inheritdoc />
    public string? Get(string name) => _variables.GetValueOrDefault(name);
}
