using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    // PR 14: the workflow's 'resources' names and sizes, so a bad one fails on activation and not on the first run.
    private void ResourceDeclarations(GraphSpec spec, List<GraphFinding> found)
    {
        if (spec.Resources is not { } resources)
        {
            return;
        }

        if (resources.Database is { } database && !ResourceName.IsMatch(database.Name ?? string.Empty))
        {
            found.Add(Bad("SND-RES-006", "resources.database.name", "the database name must be lowercase letters, digits and '-', 1 to 64 characters, starting with a letter or digit", "rename resources.database.name"));
        }

        if (resources.Sandbox is { } sandbox)
        {
            SandboxMemory(sandbox, found);
            SandboxWall(sandbox, found);
        }
    }

    private void SandboxMemory(GraphSandbox sandbox, List<GraphFinding> found)
    {
        if (sandbox.Memory is not null)
        {
            if (!ResourceSizes.TryParse(sandbox.Memory, out var bytes))
            {
                found.Add(Bad("SND-RES-006", "resources.sandbox.memory", $"the sandbox memory '{sandbox.Memory}' is a whole number and k, m, g or t (or KiB to TiB), such as 4g", "write the memory as 4g or 512m"));
            }
            else if (context.BoxCap is { } cap && bytes > cap.MemoryBytes)
            {
                found.Add(Bad("SND-RES-005", "resources.sandbox.memory", $"the sandbox memory {SandboxUnits.FormatSize(bytes)} is above this box's cap of {SandboxUnits.FormatSize(cap.MemoryBytes)}", $"lower resources.sandbox.memory to {SandboxUnits.FormatSize(cap.MemoryBytes)} or less"));
            }
        }
    }

    private void SandboxWall(GraphSandbox sandbox, List<GraphFinding> found)
    {
        if (sandbox.Wall is not null)
        {
            if (!RecipeDuration.TryParse(sandbox.Wall, provider: null, out var wall))
            {
                found.Add(Bad("SND-RES-006", "resources.sandbox.wall", $"the sandbox wall '{sandbox.Wall}' is a whole number and m, h or d, such as 45m", "write the wall as 45m or 2h"));
            }
            else if (context.BoxCap is { } cap && wall.Span > cap.Wall)
            {
                found.Add(Bad("SND-RES-005", "resources.sandbox.wall", $"the sandbox wall {sandbox.Wall} is above this box's cap of {FormatWall(cap.Wall)}", $"lower resources.sandbox.wall to {FormatWall(cap.Wall)} or less"));
            }
        }
    }

    private static GraphFinding Bad(string code, string field, string message, string fix) => new(code, GraphSeverity.Error, [], field, message, fix);

    private static string FormatWall(TimeSpan wall) => wall.TotalMinutes % 60 == 0 ? string.Create(CultureInfo.InvariantCulture, $"{(int)wall.TotalHours}h") : string.Create(CultureInfo.InvariantCulture, $"{(int)wall.TotalMinutes}m");

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ResourceName { get; }
}
