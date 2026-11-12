using System.Text.Json;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The #2868 red: every todos-note golden host writes its note at one fixed temp path, so a second host's import
/// finds the first host's "^td-" ids in the note and adopts nothing. Two hosts started before either runs, then run one after the other.
/// </summary>
public sealed class TodoNoteVaultIsolationTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task Two_todos_note_import_hosts_each_answer_imported_2()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await TodoScenarios.Start("todos-note-import", ct);
        await using var second = await TodoScenarios.Start("todos-note-import", ct);

        var a = await TodoScenarios.Body(first, "todos-note-import", "imported", ct);
        var b = await TodoScenarios.Body(second, "todos-note-import", "imported", ct);

        Assert.Equal(2, Imported(a));
        Assert.True(Imported(b) == 2, $"the second host's import saw the first host's note; its answer was: {b}");
    }

    private static int Imported(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("imported").GetInt32();
    }
}
