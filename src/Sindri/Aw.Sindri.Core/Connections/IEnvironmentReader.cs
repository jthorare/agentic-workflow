namespace Aw.Sindri.Core.Connections;

/// <summary>Reads one environment variable (for --key-env NAME: the value is read once and stored as a secret).</summary>
public interface IEnvironmentReader
{
    /// <summary>Reads a variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or null when unset or empty.</returns>
    public string? Get(string name);
}
