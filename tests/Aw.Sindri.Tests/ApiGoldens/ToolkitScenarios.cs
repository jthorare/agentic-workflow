using Aw.Sindri.Core.Toolkits;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of the toolkit routes (agent-first PR 16c): 'GET /sindri/toolkits', 'POST /sindri/toolkits' (create, a job), 'POST /sindri/toolkits/build' (a job), the four
/// 'POST /sindri/toolkits/{name}/start|stop|pause|resume' commands and 'GET /sindri/toolkits/{name}/calls'. A scenario seeds the rows it needs through the host's toolkit store; no
/// process starts, so start, pause and resume are refused or answered from the row. Every request is the owner's.
/// </summary>
internal static class ToolkitScenarios
{
    private const string Manifest = """{"name":"notes","version":"1.0.0","resources":[],"tools":[{"name":"add_note","description":"Adds a note.","inputSchema":{"type":"object"},"outputSchema":{"type":"object"}}]}""";

    private static readonly (string Name, ToolkitState State)[] _notes = [("notes", ToolkitState.Built)];
    private static readonly (string Name, ToolkitState State)[] _notesOff = [("notes", ToolkitState.Off)];

    // slug, scenario, the rows to seed, the method, the path and the body
    private static readonly (string Slug, string Scenario, (string Name, ToolkitState State)[] Seed, string Method, string Path, string? Body)[] _rows =
    [
        ("toolkits", "empty", [], "GET", "/sindri/toolkits", null),
        ("toolkits", "listed", [("notes", ToolkitState.Built), ("scratch", ToolkitState.Off)], "GET", "/sindri/toolkits", null),
        ("toolkits", "created", [], "POST", "/sindri/toolkits", """{"name":"notes","tool":"add_note"}"""),
        ("toolkits-build", "started", _notesOff, "POST", "/sindri/toolkits/build", """{"name":"notes"}"""),
        ("toolkits-name-start", "not-found", [], "POST", "/sindri/toolkits/missing/start", null),
        ("toolkits-name-start", "not-built", _notesOff, "POST", "/sindri/toolkits/notes/start", null),
        ("toolkits-name-stop", "not-found", [], "POST", "/sindri/toolkits/missing/stop", null),
        ("toolkits-name-stop", "stopped", _notes, "POST", "/sindri/toolkits/notes/stop", null),
        ("toolkits-name-pause", "not-found", [], "POST", "/sindri/toolkits/missing/pause", null),
        ("toolkits-name-pause", "not-running", _notes, "POST", "/sindri/toolkits/notes/pause", null),
        ("toolkits-name-resume", "not-found", [], "POST", "/sindri/toolkits/missing/resume", null),
        ("toolkits-name-resume", "not-running", _notes, "POST", "/sindri/toolkits/notes/resume", null),
        ("toolkits-name-calls", "empty", _notes, "GET", "/sindri/toolkits/notes/calls?limit=5", null),
    ];

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [.. _rows.Select(static r => (r.Slug, r.Scenario))];

    /// <summary>Whether this class owns the scenario.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True when it does.</returns>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the answer's body.</summary>
    /// <param name="host">The golden host.</param>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The body.</returns>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        var row = _rows.Single(r => r.Slug == slug && r.Scenario == scenario);
        foreach (var (name, state) in row.Seed)
        {
            await Seed(host, name, state, ct);
        }

        var owner = Human(host.Token.Value);
        var request = row.Method == "GET" ? Get(row.Path, owner) : Post(row.Path, row.Body, owner);
        return (await host.Http.Ask(request)).Body;
    }

    private static async Task Seed(GoldenHost host, string name, ToolkitState state, CancellationToken ct)
    {
        var built = state == ToolkitState.Off ? null : ToolkitManifest.Parse(Manifest).Value;
        var toolkit = new Toolkit(name, "/toolkits/" + name, state, built is null ? null : "hash-1", built is null ? null : host.Now(), built, "owner");
        _ = (await host.App.Services.GetRequiredService<IToolkitStore>().Save(toolkit, ct)).Value;
    }
}
