namespace Aw.Context.Core.Engine;

/// <summary>What the library's path rules need from the machine, passed in because the core reads no environment.</summary>
public sealed class LibraryHost
{
    /// <summary>The owner's home folder, which a leading '~/' expands to.</summary>
    public required string Home { get; init; }

    /// <summary>$AW_SINDRI_HOME: a vault may not hold it. Null when unknown.</summary>
    public string? SindriHome { get; init; }

    /// <summary>The 'aw' root when 'stateDir' is left out: $AW_STATE_DIR, else ~/.agentic-workflow.</summary>
    public required string StateDefault { get; init; }

    /// <summary>Gives the real path of an absolute path (symlinks resolved where they exist); roots are compared by it. The default leaves the path as it is.</summary>
    public Func<string, string> RealPath { get; init; } = static path => path;
}
