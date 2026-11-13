using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Todos;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>
/// The server's real answers on a home with no profile, serialized through Sindri's JSON context, equal the files in Golden/FreshHome. The app's test
/// (Aw.Sindri.Ui.Tests FreshHomeWireTests) reads the same files through its own JSON context, so a field name can't drift on one side only (#3113).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FreshHomeWireTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Fixture(string name) =>
        JsonNode.Parse(File.ReadAllText(Golden.SourcePath(typeof(FreshHomeWireTests).Assembly, "Golden/FreshHome/" + name)))!;

    [Fact]
    public async Task The_status_profile_of_a_home_with_no_profile_matches_the_file_the_app_reads()
    {
        var h = new SliceHarness();
        h.Profiles.Live = SindriErrors.ProfileMissing.With("No profile found");
        var service = new GetStatusService(h.Held, new ProfileReads(h.Profiles, h.Profiles), h.Store, new ProjectorProgress(h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

        var status = (await service.Handle(new GetStatusRequest(), Token)).Value;

        var served = JsonNode.Parse(AwJson.Serialize(status, SliceHarness.Json))!["profile"];
        Assert.True(JsonNode.DeepEquals(Fixture("profile.json"), served), served?.ToJsonString());
    }

    [Fact]
    public async Task The_todo_note_of_a_home_with_no_profile_matches_the_file_the_app_reads()
    {
        var h = new TodoHarness();
        h.NoteLocator.Fails = SindriErrors.ProfileMissing.With("No profile found");
        await h.Lease();
        using var cli = h.Cli();

        var note = (await h.Note().Handle(new GetTodoNoteRequest(), Token)).Value;

        var served = JsonNode.Parse(AwJson.Serialize(note, SliceHarness.Json))!.AsObject();
        var expected = Fixture("todo-note.json").AsObject();
        _ = served.Remove("asOf");   // why: the views' clock, not what this test pins
        _ = expected.Remove("asOf");
        Assert.True(JsonNode.DeepEquals(expected, served), served.ToJsonString());
    }
}
