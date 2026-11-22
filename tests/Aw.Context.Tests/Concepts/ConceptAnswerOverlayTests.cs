using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Context.Features.GetConcepts;
using Aw.Context.Tests.GetHistory;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Concepts;

/// <summary>
/// Issue 3676 with #3760: a kept concepts answer holds only what the map version and the profile decide. A concept's lastChanged comes from git, outside the answer's key,
/// so it is laid over the kept rows on every read: a time git learns after the answer was kept still shows, and the kept row never freezes a time.
/// </summary>
[Trait("Lane", "unit")]
public sealed class ConceptAnswerOverlayTests : IDisposable
{
    private static readonly ContextScope App = MapWorld.HumanVia(ContextClient.Http);
    private static readonly DateTimeOffset _changed = new(2026, 5, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly MapWorld _world;

    public ConceptAnswerOverlayTests() =>
        _world = new MapWorld(history: new ScriptedHistory(inner: null!, (_, query, _) => ValueTask.FromResult(Answer(query))));

    public void Dispose() => _world.Dispose();

    private static Result<FileHistory> Answer(FileHistoryQuery query)
    {
        string[] files = [.. query.Paths.Where(p => p == "src/shifts.ts")];
        return new FileHistory(query.UpTo ?? MapWorld.Head, Shallow: false, [new FileChange(query.UpTo ?? MapWorld.Head, _changed, "Touch shifts", 1, files)], Truncated: false);
    }

    private static string Yaml() => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"concept:scheduling\", label: \"Self scheduling\", attributes: {summary: \"People pick shifts.\", source: derived}}",
        "  - {key: \"feature:shifts\", label: \"Shifts\"}",
        "  - {key: \"code:src/shifts.ts\", label: \"shifts.ts\", provenance: deterministic}",
        "edges:",
        "  - {from: \"feature:shifts\", to: \"concept:scheduling\", type: belongs-to}",
        "  - {from: \"code:src/shifts.ts\", to: \"feature:shifts\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/shifts.ts\"}]}");

    private async Task<DateTimeOffset?> Scheduling()
    {
        var view = await _world.Engine.Concepts(new ConceptsQuery(MapRead.Current, new ConceptPage()), App, MapWorld.None);
        Assert.True(view.IsSuccess, view.IsError ? view.Error.Message : null);
        return view.Value.Concepts.Single(c => c.Id.Value == "concept:scheduling").LastChanged;
    }

    [Fact]
    public async Task A_kept_answer_shows_the_change_time_git_learns_after_it_was_kept()
    {
        _ = await _world.ImportYaml(Yaml());   // the import keeps the app's answer before git was asked

        var cold = await Scheduling();
        await _world.Service<ConceptChangeTimes>().WhenIdle();
        var warm = await Scheduling();

        Assert.Null(cold);
        Assert.Equal(_changed, warm);
    }
}
