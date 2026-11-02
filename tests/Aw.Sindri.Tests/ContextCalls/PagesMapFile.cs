using Aw.Context.Testing.Fixtures;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>A map with one suggested page (the route 'GET /schedule/week' with two actions) and two concepts: what the pasted-pages preview matches against.</summary>
internal static class PagesMapFile
{
    private const string Week = "surface:route/GET:/schedule/week";

    /// <summary>Writes the map as YAML under <paramref name="directory"/> and answers its path; the repo is the tabs fixture's.</summary>
    /// <param name="directory">The host's home.</param>
    /// <returns>The file's path.</returns>
    internal static string Write(string directory)
    {
        var file = Path.Combine(directory, "pages-map.yaml");
        File.WriteAllText(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: " + MapTabsRepo.Repo, "nodes:",
            "  - {key: \"concept:self-scheduling\", label: \"Self-scheduling\", attributes: {summary: \"People pick shifts.\", source: derived}}",
            "  - {key: \"concept:approval-workflow\", label: \"Approval workflow\", attributes: {summary: \"Managers approve.\", source: derived}}",
            $"  - {{key: \"{Week}\", label: \"GET /schedule/week\", provenance: deterministic, evidence: [{{key: \"repo:{MapTabsRepo.Repo}/src/Week.tsx\"}}]}}",
            $"  - {{key: \"action:WeekPage.submit\", label: \"submit\", provenance: deterministic, evidence: [{{key: \"repo:{MapTabsRepo.Repo}/src/Week.tsx\"}}]}}",
            $"  - {{key: \"action:WeekPage.cancel\", label: \"cancel\", provenance: deterministic, evidence: [{{key: \"repo:{MapTabsRepo.Repo}/src/Week.tsx\"}}]}}",
            "  - {key: \"code:src/Week.tsx\", label: \"Week.tsx\", provenance: deterministic}",
            "edges:",
            $"  - {{from: \"code:src/Week.tsx\", to: \"{Week}\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:{MapTabsRepo.Repo}/src/Week.tsx\"}}]}}",
            $"  - {{from: \"code:src/Week.tsx\", to: \"action:WeekPage.submit\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:{MapTabsRepo.Repo}/src/Week.tsx\"}}]}}",
            $"  - {{from: \"action:WeekPage.submit\", to: \"{Week}\", type: on, provenance: deterministic}}",
            $"  - {{from: \"action:WeekPage.cancel\", to: \"{Week}\", type: on, provenance: deterministic}}",
            "  - {from: \"action:WeekPage.submit\", to: \"concept:self-scheduling\", type: expresses, provenance: deterministic}") + "\n");
        return file;
    }
}
