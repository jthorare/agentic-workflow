using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>IKeychainLogin over the 'security' command: 'find-generic-password -s "Claude Code-credentials" -w' prints the item's JSON on standard output.</summary>
/// <param name="processes">Runs the command.</param>
/// <param name="isMac">Whether this is macOS; off macOS the command is never run.</param>
internal sealed class SecurityCliKeychainLogin(ISandboxProcesses processes, bool isMac) : IKeychainLogin
{
    internal const string ItemName = "Claude Code-credentials";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public string? Read()
    {
        if (!isMac)
        {
            return null;
        }

        var result = processes.Run("security", ["find-generic-password", "-s", ItemName, "-w"], _timeout);
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
