using System.Globalization;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// What an agent is told at launch about Sindri's MCP endpoint (launcher spec section 3, the entry's 'mcp' form): Claude Code gets a 0600 file under
/// <c lang="text">launch/</c> that is deleted with the launch, Codex gets typed '-c' overrides. Neither carries a token: the session reaches the
/// loopback endpoint with the repo header and its own launch id (X-Sindri-Launch, which tells the API a Sindri child from an owner-started session; q100). For an agent Step's launch also its instance and run, which scope the context tools to the Step's graph.
/// </summary>
internal static class McpLaunchConfig
{
    private const string RepoHeader = "X-Sindri-Repo";
    private const string InstanceHeader = "X-Sindri-Instance";
    private const string RunHeader = "X-Sindri-Run";
    private const string LaunchHeader = "X-Sindri-Launch";

    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(LauncherJsonContext.Default, indented: true);

    /// <summary>The endpoint a session is given.</summary>
    /// <param name="port">Sindri's API port.</param>
    /// <returns>The loopback URL.</returns>
    internal static string Url(int port) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/sindri/mcp");

    /// <summary>The Claude Code config file's text.</summary>
    /// <param name="target">The endpoint and repo.</param>
    /// <param name="launch">The launch the file is for.</param>
    /// <returns>The JSON.</returns>
    internal static byte[] Render(McpTarget target, LaunchId launch)
    {
        ArgumentNullException.ThrowIfNull(target);
        var server = new McpServerEntry("http", Url(target.Port), Headers(target, launch));
        var document = new McpConfigDocument(new Dictionary<string, McpServerEntry>(StringComparer.Ordinal) { [McpScope.ServerName] = server });
        return [.. AwJson.Serialize(document, _json), .. "\n"u8];
    }

    // The repo and launch headers always; the instance and run headers only for an agent Step's launch (both set).
    private static Dictionary<string, string> Headers(McpTarget target, LaunchId launch)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { [RepoHeader] = target.Repo, [LaunchHeader] = launch.Value };
        if (target.Instance is { Length: > 0 } instance && target.Run is { Length: > 0 } run)
        {
            headers[InstanceHeader] = instance;
            headers[RunHeader] = run;
        }

        return headers;
    }

    /// <summary>Where the launch's config file lives under <paramref name="directory"/>.</summary>
    /// <param name="directory">The launcher's <c lang="text">launch/</c> directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The file's path.</returns>
    internal static string PathFor(string directory, LaunchId launch) => Path.Combine(directory, "mcp-" + launch.Value + ".json");

    /// <summary>Writes the file 0600 through a temp file and a rename.</summary>
    /// <param name="directory">The launcher's <c lang="text">launch/</c> directory.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="target">The endpoint and repo.</param>
    /// <returns>The file's path.</returns>
    internal static string Write(string directory, LaunchId launch, McpTarget target)
    {
        OwnerOnly.EnsureDirectory(directory);
        var path = PathFor(directory, launch);
        OwnerOnly.WriteAtomically(path, Render(target, launch));
        return path;
    }

    /// <summary>Deletes the launch's file; a missing one is fine.</summary>
    /// <param name="directory">The launcher's <c lang="text">launch/</c> directory.</param>
    /// <param name="launch">The launch.</param>
    internal static void Delete(string directory, LaunchId launch) => _ = OwnerOnly.TryDelete(PathFor(directory, launch));

    /// <summary>Deletes every config file older than <paramref name="olderThan"/> whose launch <paramref name="live"/> doesn't name (a crashed host leaves them).</summary>
    /// <param name="directory">The launcher's <c lang="text">launch/</c> directory.</param>
    /// <param name="now">Now.</param>
    /// <param name="olderThan">How old a file must be.</param>
    /// <param name="live">True for a launch that still runs.</param>
    /// <returns>How many files were removed.</returns>
    internal static int Sweep(string directory, DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live)
    {
        ArgumentNullException.ThrowIfNull(live);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "mcp-LCH-*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file)["mcp-".Length..];
            if (LaunchId.TryParse(name, provider: null, out var launch) && now - new DateTimeOffset(File.GetLastWriteTimeUtc(file)) > olderThan && !live(launch))
            {
                if (OwnerOnly.TryDelete(file))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>A TOML basic string: quoted, with the backslash and the quote escaped and control characters written as \uXXXX, so a value can't end the string or the table early.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The quoted string.</returns>
    internal static string Toml(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var text = new System.Text.StringBuilder("\"");
        foreach (var c in value)
        {
            _ = c switch
            {
                '\\' => text.Append("\\\\"),
                '"' => text.Append("\\\""),
                < ' ' or '\u007f' => text.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}"),
                _ => text.Append(c),
            };
        }

        return text.Append('"').ToString();
    }

    /// <summary>The arguments that give the agent its endpoint, after its expanded open or resume template.</summary>
    /// <param name="form">The entry's MCP form.</param>
    /// <param name="target">The endpoint and repo.</param>
    /// <param name="launch">The launch the arguments are for.</param>
    /// <param name="configPath">The written config file, for the Claude form.</param>
    /// <returns>The arguments; none for <see cref="McpForm.None"/>.</returns>
    internal static IReadOnlyList<string> Arguments(McpForm form, McpTarget target, LaunchId launch, string? configPath)
    {
        ArgumentNullException.ThrowIfNull(target);
        return form switch
        {
            McpForm.ClaudeConfigFile when configPath is not null => ["--mcp-config", configPath],
            McpForm.CodexOverrides =>
            [
                "-c", $"mcp_servers.{McpScope.ServerName}.url={Toml(Url(target.Port))}",
                "-c", $"mcp_servers.{McpScope.ServerName}.http_headers={{ {string.Join(", ", Headers(target, launch).Select(static h => $"{h.Key} = {Toml(h.Value)}"))} }}",
            ],
            McpForm.None or McpForm.ClaudeConfigFile => [],
            _ => [],
        };
    }

    /// <summary>Writes what the entry's form needs and returns the file path (Claude form) and the arguments to add.</summary>
    /// <param name="form">The entry's MCP form.</param>
    /// <param name="target">The endpoint and repo, or null when none is known (nothing is added).</param>
    /// <param name="directory">The launcher's <c lang="text">launch/</c> directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The path of the written file, or null, and the extra arguments.</returns>
    internal static (string? ConfigPath, IReadOnlyList<string> Arguments) Prepare(McpForm form, McpTarget? target, string directory, LaunchId launch)
    {
        if (target is null || form == McpForm.None)
        {
            return (null, []);
        }

        var path = form == McpForm.ClaudeConfigFile ? Write(directory, launch, target) : null;
        return (path, Arguments(form, target, launch, path));
    }
}
