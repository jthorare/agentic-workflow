using Aw.Judge.Core.Ids;
using Aw.Judge.Core.PromptSort;
using Aw.Judge.Infrastructure.PromptSort;

namespace Aw.Judge.Tests.SortPrompt;

// What only the file adapter has: done-gate.sh reads and rewrites 'sessions/<sid>.sort.json', so keys this build doesn't
// know survive a write, the replace leaves no temp file, and a file a TS sorter left behind reads as is.
public sealed class FileSortSessionStoreTests : IDisposable
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly SessionId _session = SessionId.From("sess-1");

    private readonly TempStateDirectory _state = new();

    private string Sessions => Path.Combine(_state.Path, "judge", "sessions");

    private string StateFile => Path.Combine(Sessions, "sess-1.sort.json");

    private FileSortSessionStore Store() => new(Sessions);

    [Fact, Trait("Lane", "integration")]
    public async Task A_write_keeps_the_keys_it_does_not_know_and_the_requirements_it_does_not_set()
    {
        _ = Directory.CreateDirectory(Sessions);
        await File.WriteAllTextAsync(StateFile, """{"prompts":1,"lastFired":{},"requirements":{"other":true},"extra":"kept"}""", _ct);

        var written = await Store().Write(_session, new SortSession(2, new Dictionary<string, int>(StringComparer.Ordinal) { ["brief"] = 2 }, new SortRequirements(UiEvidence: true)), _ct);

        Assert.True(written.IsSuccess);
        var file = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(StateFile, _ct))!;   // why: the file was just written
        Assert.Equal("kept", (string?)file["extra"]);
        Assert.True((bool?)file["requirements"]!["other"]);   // why: the file was just written
        Assert.True((bool?)file["requirements"]!["uiEvidence"]);   // why: the file was just written
        Assert.Equal(2, (int?)file["prompts"]);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_write_drops_the_ui_requirement_when_the_state_has_none_and_leaves_no_temp_file()
    {
        _ = Directory.CreateDirectory(Sessions);
        await File.WriteAllTextAsync(StateFile, """{"prompts":1,"lastFired":{},"requirements":{"uiEvidence":true}}""", _ct);

        _ = await Store().Write(_session, new SortSession(2, new Dictionary<string, int>(StringComparer.Ordinal), new SortRequirements()), _ct);

        Assert.DoesNotContain("uiEvidence", await File.ReadAllTextAsync(StateFile, _ct), StringComparison.Ordinal);
        Assert.Equal([StateFile], Directory.GetFiles(Sessions));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_file_a_ts_sorter_left_behind_reads_as_is()
    {
        _ = Directory.CreateDirectory(Sessions);
        await File.WriteAllTextAsync(StateFile, """{"prompts":4,"lastFired":{"brief":3,"bugfix":4},"requirements":{"uiEvidence":true}}""", _ct);

        var read = await Store().Read(_session, _ct);

        Assert.Equal(4, read.Value.Prompts);
        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal) { ["brief"] = 3, ["bugfix"] = 4 }, read.Value.LastFired);
        Assert.True(read.Value.Requirements.UiEvidence);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("""{"prompts":"two"}""")]
    [InlineData("[]")]
    public async Task A_file_that_is_not_a_state_reads_as_empty(string content)
    {
        _ = Directory.CreateDirectory(Sessions);
        await File.WriteAllTextAsync(StateFile, content, _ct);

        var read = await Store().Read(_session, _ct);

        Assert.True(read.IsSuccess);
        Assert.Equal(0, read.Value.Prompts);
    }

    public void Dispose() => _state.Dispose();
}
