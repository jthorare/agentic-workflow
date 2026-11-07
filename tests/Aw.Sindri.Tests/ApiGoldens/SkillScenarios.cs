using Aw.Sindri.Testing.Skills;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// F-1's golden for 'GET /sindri/skills' (skill flows spec §7): the host loads the fixture skills folder (review, and officeHours with two blocks) through AW_SINDRI_SKILLS_DIR, and the answer lists
/// each block with its descriptor, version, inputs, outputs, outcomes, SKILL.md sha and the path it came from.
/// </summary>
internal static class SkillScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [("skills", "fixtures-loaded")];

    /// <summary>The fixture folder's path as served, masked in the golden so it doesn't name this machine.</summary>
    public static string FolderMask => SkillFixtures.Folder;

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts a host that loads the fixture skills folder.</summary>
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_SKILLS_DIR"] = SkillFixtures.Folder });

    /// <summary>Asks for the loaded blocks and returns the body.</summary>
    public static async Task<string> Body(GoldenHost host, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return (await host.Http.Ask(Get("/sindri/skills", Human(host.Token.Value)))).Body;
    }
}
