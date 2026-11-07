using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>The agent home on disk: what a skill launch's requires.yaml and SKILL.md checks read (SND-SKILL-005/007). Claude Code keeps agents as '&lt;home&gt;/agents/&lt;name&gt;.md' and MCP servers in '.claude.json'; Codex keeps agents as '.toml' and MCP servers in 'config.toml'.</summary>
internal sealed partial class FileAgentHome : IAgentHome
{
    private readonly SkillTarget _target;
    private readonly string _ownerHome;
    private readonly IReadOnlyList<string> _path;

    /// <summary>Opens the agent home a launch uses.</summary>
    /// <param name="folder">The agent home: '~/.claude', the separate login's CLAUDE_CONFIG_DIR, '~/.codex'.</param>
    /// <param name="target">Which agent it is.</param>
    /// <param name="ownerHome">The owner's home; Claude Code's default '.claude.json' sits beside '~/.claude' in it.</param>
    /// <param name="path">The PATH of the launch.</param>
    internal FileAgentHome(string folder, SkillTarget target, string ownerHome, string path)
    {
        Folder = folder;
        _target = target;
        _ownerHome = ownerHome;
        _path = path.Split(':', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <inheritdoc/>
    public string Folder { get; }

    /// <inheritdoc/>
    public bool HasBin(string bin) => _path.Any(dir => ExecutableFiles.IsExecutable(Path.Combine(dir, bin)));

    /// <inheritdoc/>
    public bool HasAgent(string agent) => File.Exists(Path.Combine(Folder, "agents", agent + (_target == SkillTarget.Codex ? ".toml" : ".md")));

    /// <inheritdoc/>
    public bool HasSkill(string skill) => File.Exists(Path.Combine(Folder, "skills", skill, "SKILL.md"));

    /// <inheritdoc/>
    public bool HasMcp(string server) => _target == SkillTarget.Codex ? CodexHas(server) : ClaudeHas(server);

    /// <inheritdoc/>
    public (string Path, string? Sha) SkillMarkdown(string skill)
    {
        var folder = Path.Combine(Folder, "skills", skill);
        try
        {
            var resolved = Directory.ResolveLinkTarget(folder, returnFinalTarget: true)?.FullName ?? folder;
            var file = Path.Combine(resolved, "SKILL.md");
            return (file, File.Exists(file) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))) : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (Path.Combine(folder, "SKILL.md"), null);   // a home with no folder for the skill lacks it
        }
    }

    // why: the default home keeps its settings in '~/.claude.json' beside the folder; a CLAUDE_CONFIG_DIR keeps it inside.
    private bool ClaudeHas(string server)
    {
        var file = string.Equals(Folder.TrimEnd('/'), Path.Combine(_ownerHome, ".claude"), StringComparison.Ordinal) ? Path.Combine(_ownerHome, ".claude.json") : Path.Combine(Folder, ".claude.json");
        try
        {
            if (!File.Exists(file))
            {
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            return Registers(document.RootElement, server) || (document.RootElement.TryGetProperty("projects", out var projects) && projects.ValueKind == JsonValueKind.Object && projects.EnumerateObject().Any(p => Registers(p.Value, server)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool Registers(JsonElement scope, string server) =>
        scope.ValueKind == JsonValueKind.Object && scope.TryGetProperty("mcpServers", out var servers) && servers.ValueKind == JsonValueKind.Object && servers.TryGetProperty(server, out _);

    private bool CodexHas(string server)
    {
        var file = Path.Combine(Folder, "config.toml");
        try
        {
            return File.Exists(file) && File.ReadAllLines(file).Any(line => McpTable.Match(line) is { Success: true } m && string.Equals(m.Groups["name"].Value, server, StringComparison.Ordinal));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [GeneratedRegex("^\\s*\\[mcp_servers\\.\"?(?<name>[^\\].\"]+)\"?(\\..*)?\\]\\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex McpTable { get; }
}
