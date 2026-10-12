namespace Aw.Judge.Core.Providers;

/// <summary>A provider in the chain. Wire names in <see cref="ProviderNames"/>.</summary>
public enum ProviderName
{
    /// <summary>'rules': local and deterministic, always the last resort.</summary>
    Rules = 1,

    /// <summary>'claude-cli'.</summary>
    ClaudeCli = 2,

    /// <summary>'codex-cli'.</summary>
    CodexCli = 3,

    /// <summary>'cursor-cli'.</summary>
    CursorCli = 4,

    /// <summary>'jev': the TypeSafe API (text classes only).</summary>
    Jev = 5,
}
