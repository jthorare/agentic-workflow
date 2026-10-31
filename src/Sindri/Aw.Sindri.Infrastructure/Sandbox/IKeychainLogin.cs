namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Reads Claude Code's login from the macOS Keychain, where it lives instead of '~/.claude/.credentials.json' (sandbox spec §3.1, SB-9c).</summary>
internal interface IKeychainLogin
{
    /// <summary>Reads the login.</summary>
    /// <returns>The login's JSON, or null off macOS, when there is no item, or when what came back is not a JSON object. Never throws.</returns>
    public string? Read();
}
