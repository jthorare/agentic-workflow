namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The API's golden JSON (UI spec E-17, §18.1): what the fake serves, so a wire change fails the app's unit lane. P1's
/// goldens are Sindri's own (tests/Aw.Sindri.Tests/Golden/Api, linked into this test output as Golden/Api); routes whose
/// API phase isn't built yet are spec-shaped (Golden/SpecShaped), each to be replaced by its phase's golden.
/// </summary>
internal static class GoldenFiles
{
    /// <summary>Where the goldens sit in the test output.</summary>
    internal static string Root => Path.Combine(AppContext.BaseDirectory, "Golden");

    /// <summary>An API golden recorded by Sindri's tests.</summary>
    /// <param name="slug">The route slug ('commands', 'status', 'views-rebuild').</param>
    /// <param name="scenario">The scenario ('catalog', 'running-approved').</param>
    /// <returns>The body as served.</returns>
    internal static string Api(string slug, string scenario) => File.ReadAllText(Path.Combine(Root, "Api", slug, scenario + ".json"));

    /// <summary>A sandbox golden (sandbox spec §8): the copy, states, accessibility rows and status lines recorded by Sindri's tests.</summary>
    /// <param name="name">The file name under Golden/Sandbox ('copy.json', 'states.json').</param>
    /// <returns>The file's text.</returns>
    internal static string Sandbox(string name) => File.ReadAllText(Path.Combine(Root, "Sandbox", name));

    /// <summary>An API golden parsed, so a test reads its expected values from the file instead of repeating them.</summary>
    /// <param name="slug">The route slug.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The parsed body.</returns>
    internal static System.Text.Json.Nodes.JsonNode Node(string slug, string scenario) => System.Text.Json.Nodes.JsonNode.Parse(Api(slug, scenario))!;

    /// <summary>A body written from the API spec's documented shape, standing in until its phase writes the golden.</summary>
    /// <param name="slug">The route slug.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The body.</returns>
    internal static string SpecShaped(string slug, string scenario) => File.ReadAllText(Path.Combine(Root, "SpecShaped", slug, scenario + ".json"));

    /// <summary>Every golden in the output: its kind ('Api' or 'SpecShaped'), slug, scenario and body.</summary>
    /// <returns>The goldens.</returns>
    internal static IEnumerable<(string Kind, string Slug, string Scenario, string Body)> All() =>
        from kind in (string[])["Api", "SpecShaped"]
        let dir = Path.Combine(Root, kind)
        where Directory.Exists(dir)
        from file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
        select (kind, Path.GetFileName(Path.GetDirectoryName(file)), Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
}
