using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>
/// The stop-2 view over the files of a design packet and its plan (line spec §6.4, LN-API2): pure, so the CLI over a directory and the route over an instance's facts show the same.
/// The files are the plan packet's ('tasks.json', 'holdout.json') and the design packet's under 'design/'; the scope packet's under 'scope/', the repo's under 'repo/', the packs' under
/// 'pack/', 'options.json' and 'freezeRefused.json' are read when they are there. A section whose files are absent is empty and named in notYet.
/// </summary>
internal static partial class DesignStopViewBuilder
{
    /// <summary>Builds the view.</summary>
    /// <param name="files">The files as the request names them.</param>
    internal static DesignStopView Build(LineFacts files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var design = DesignFiles(files);
        var options = files.Json("options.json");
        var tasks = Phases(files);
        var graph = Graph(files, options);
        return new DesignStopView(
            NotYet(files),
            Accepted(design),
            Models(design),
            Decisions(design),
            Acceptance(design),
            Escapes(design),
            graph,
            Trace(files, design, tasks),
            Coverage(design),
            Spec(design),
            Contracts(design),
            LensRounds(design),
            FreezeRefused(files));
    }

    /// <summary>The design packet's files at the root, where the design checks read them, with the scope, pack and repo files and the options beside them.</summary>
    private static LineFacts DesignFiles(LineFacts files)
    {
        var rekeyed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in files.Names)
        {
            if (name.StartsWith("design/", StringComparison.Ordinal))
            {
                rekeyed[name["design/".Length..]] = files.Text(name);
            }
            else if (name.StartsWith("scope/", StringComparison.Ordinal) || name.StartsWith("pack/", StringComparison.Ordinal) || name.StartsWith("repo/", StringComparison.Ordinal) || name == "options.json")
            {
                rekeyed[name] = files.Text(name);
            }
        }

        return new LineFacts(rekeyed);
    }

    // The sections with no data yet, in the order the view lists them.
    private static List<string> NotYet(LineFacts files)
    {
        var notYet = new List<string>();
        void When(bool missing, string section)
        {
            if (missing)
            {
                notYet.Add(section);
            }
        }

        When(!files.Has("design/lens-rounds.jsonl"), "accepted");
        When(!files.Has("design/runs.json"), "models");
        When(!files.Has("design/decisions.json"), "decisions");
        When(!files.Has("design/acceptance.json"), "acceptance");
        When(!files.Has("design/escapes.json"), "escapes");
        When(!files.Has("tasks.json"), "graph");
        When(!files.Has("tasks.json") || !files.Has("design/signals.json"), "trace");
        When(!files.Has("design/coverage.json"), "coverage");
        When(!files.Has("design/spec.md"), "spec");
        When(!files.Has("design/contracts.json"), "contracts");
        When(!files.Has("design/lens-rounds.jsonl"), "lensRounds");
        return notYet;
    }

    private static StopFreezeRefused? FreezeRefused(LineFacts files) =>
        files.Json("freezeRefused.json") is { } refused && LineJson.Str(refused, "rule") is { } rule ? new StopFreezeRefused(rule, LineJson.Str(refused, "message") ?? string.Empty) : null;

    private static string Text(JsonNode? node, string name) => LineJson.Str(node, name) ?? string.Empty;
}
