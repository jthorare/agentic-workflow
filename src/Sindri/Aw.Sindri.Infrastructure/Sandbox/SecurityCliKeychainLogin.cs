using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>IKeychainLogin over the 'security' command: 'find-generic-password -s "Claude Code-credentials" -w' prints the item's JSON on standard output.</summary>
/// <param name="processes">Runs the command.</param>
/// <param name="isMac">Whether this is macOS; off macOS the command is never run.</param>
internal sealed class SecurityCliKeychainLogin(ISandboxProcesses processes, bool isMac) : IKeychainLogin
{
    internal const string ItemName = "Claude Code-credentials";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>The Keychain service Claude Code stores its login under for a config directory.</summary>
    /// <param name="configDirectory">CLAUDE_CONFIG_DIR, or null when it is not set.</param>
    /// <returns>The service name.</returns>
    internal static string ServiceFor(string? configDirectory)
    {
        _ = configDirectory;   // why: stub until the fix (#1928)
        return ItemName;
    }

    /// <inheritdoc />
    public string? Read(string service)
    {
        if (!isMac)
        {
            return null;
        }

        var result = processes.Run("security", ["find-generic-password", "-s", service, "-w"], _timeout);
        if (!result.Succeeded)
        {
            return null;
        }

        var json = result.Output.Trim();
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? json : null;
        }
        catch (JsonException)
        {
            return null;   // why: nothing of the output is kept or reported
        }
    }
}
