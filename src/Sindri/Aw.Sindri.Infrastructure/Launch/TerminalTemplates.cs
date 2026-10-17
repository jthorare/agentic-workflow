using Aw.Kernel.Json;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The rows of <c lang="text">config/launcher/terminals.json</c>, embedded in this assembly (once, beside the built-in agents).</summary>
internal static class TerminalTemplates
{
    internal static IReadOnlyList<TerminalTemplate> Load()
    {
        using var stream = typeof(TerminalTemplates).Assembly.GetManifestResourceStream("terminals.json")
            ?? throw new InvalidOperationException("terminals.json isn't embedded in Aw.Sindri.Infrastructure");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        var file = AwJson.Deserialize<TerminalTemplatesFile>(copy.ToArray(), LauncherConfigFile.Json).Value;
        return [.. file.Terminals.Select(static t => new TerminalTemplate(t.Id, t.Name, t.Bin, t.TermProgram ?? [], t.Argv))];
    }
}
