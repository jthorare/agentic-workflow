using System.Globalization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Mcp;

/// <summary>
/// The claude arguments of the registration Sindri makes (context spec §3, API spec §8): local scope, inside the work
/// repo's checkout, with the repo header the endpoint scopes by. Codex and Cursor get project files instead.
/// </summary>
public static class McpRegistration
{
    /// <summary>The claude CLI's arguments for <paramref name="repo"/> on <paramref name="port"/>.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="port">Sindri's API port.</param>
    /// <returns>The arguments after 'claude'; the header goes last, since claude's '--header &lt;header...&gt;' takes every later argument.</returns>
    public static IReadOnlyList<string> Arguments(RepoName repo, int port) =>
        ["mcp", "add", "--transport", "http", "--scope", "local", McpScope.ServerName, Url(port), "--header", "X-Sindri-Repo: " + repo.Value];

    /// <summary>The /sindri/mcp URL on <paramref name="port"/>, the one every provider is given.</summary>
    /// <param name="port">Sindri's API port.</param>
    /// <returns>The URL.</returns>
    public static string Url(int port) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/sindri/mcp");

    /// <summary>The command as one shell line ('claude' and each argument, quoted where it needs it).</summary>
    /// <param name="arguments">The arguments after 'claude'.</param>
    /// <returns>The line.</returns>
    public static string CommandLine(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return "claude " + string.Join(' ', arguments.Select(a => a.Any(c => char.IsWhiteSpace(c) || c is '\'' or '"' or '$' or '\\' or '`') ? "'" + a.Replace("'", "'\\''", StringComparison.Ordinal) + "'" : a));
    }

    /// <summary>The port to register: the running instance's, else the profile's API port.</summary>
    /// <param name="running">The running instance's URL, or null when none is running.</param>
    /// <param name="profilePort">The approved profile's api.port, or null.</param>
    /// <returns>The port.</returns>
    public static int Port(Uri? running, int? profilePort) =>
        running is { IsDefaultPort: false } url ? url.Port : profilePort ?? Profile.ApiSettings.DefaultPort;
}
