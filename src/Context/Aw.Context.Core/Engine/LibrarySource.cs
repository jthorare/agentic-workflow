namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.library', checked (review page spec §5.1): the state folder's docs and the vaults the library feed reads.</summary>
public sealed class LibrarySource
{
    /// <summary>The value of 'stateDir' that turns the 'aw' root off.</summary>
    public const string Off = "off";

    /// <summary>'stateDir' as written; null follows the host's default ($AW_STATE_DIR, else ~/.agentic-workflow), <see cref="Off"/> turns the root off.</summary>
    public string? StateDir { get; init; }

    /// <summary>Whether docs of the 'aw' root may reach a model call; false by default.</summary>
    public bool Models { get; init; }

    /// <summary>The vaults, at most 10.</summary>
    public IReadOnlyList<LibraryVault> Vaults { get; init; } = [];

    /// <summary>Whether the 'aw' root is read.</summary>
    public bool AwRootOn => !string.Equals(StateDir, Off, StringComparison.Ordinal);
}
