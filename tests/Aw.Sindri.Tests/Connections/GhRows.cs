using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The `github:gh` row the accounts step writes for a gh login (first-run spec decision 14), and a command runner that answers its credential.</summary>
internal static class GhRows
{
    /// <summary>The token the fake gh prints.</summary>
    public const string Token = "gho-from-gh";

    /// <summary>A connected `github:gh` row with no secret pointer.</summary>
    /// <param name="createdAt">When it was added; null is an hour after the samples' epoch.</param>
    /// <returns>The row.</returns>
    public static Connection Row(DateTimeOffset? createdAt = null)
    {
        var at = createdAt ?? KnownInstants.Epoch.AddHours(1);
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { [GitHubCliCredential.ConfigKey] = GitHubCliCredential.Command, ["login"] = "ada" };
        return new Connection(GitHubCliCredential.ConnectionName, ConnectionKind.Cli, "github", config, SecretPointer: null, ConnectionStatus.Connected, StatusReason: null, "human:app", at, at, LastRefreshedAt: null);
    }

    /// <summary>Answers `command:gh auth token` with <see cref="Token"/>; anything else with null.</summary>
    /// <param name="signedIn">False plays a gh that was signed out after the connect: it prints nothing.</param>
    internal sealed class Commands(bool signedIn = true) : ICommandCredentials
    {
        public string? Token(string credential) => signedIn && string.Equals(credential, GitHubCliCredential.Command, StringComparison.Ordinal) ? GhRows.Token : null;
    }
}
