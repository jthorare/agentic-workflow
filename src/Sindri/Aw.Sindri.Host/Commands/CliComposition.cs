namespace Aw.Sindri.Host.Commands;

/// <summary>Which container a CLI verb runs in (#3727).</summary>
internal enum CliComposition
{
    /// <summary>No container: 'help', '--help' and '-h' print the usage.</summary>
    None,

    /// <summary>Composition.ComposeClient: the verb only calls the running control plane (or starts it).</summary>
    Client,

    /// <summary>Composition.Compose: the verb runs a handler, a store or an engine in this process.</summary>
    Full,
}
