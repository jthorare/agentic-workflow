using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The Projects page is open to agent sessions (launched or not): each write succeeds and its event names the session as the actor.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectWriteTests
{
    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> Callers => ["session", "launched"];

    private IDisposable Enter(string caller) => _h.Slice.Callers.Enter(caller == "session" ? A.Caller().Session().Build() : A.Caller().Launched().Build());

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task A_session_may_edit_the_Projects_page_and_the_event_names_it(string caller)
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var entered = Enter(caller);

        var attach = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen"), Token);
        var change = await _h.Change().Handle(new ChangeProjectRequest(ProjectRows.HandMadeId.Value, 1, new ProjectEdit(State: ProjectState.Paused)), Token);
        var detach = await _h.Detach().Handle(new DetachProjectRequest(ProjectRows.HandMadeId, 1), Token);
        var hide = await _h.Hide().Handle(new HideProjectRequest(ProjectRows.LinearKey, Hidden: true, Version: 0), Token);

        Assert.True(attach.IsSuccess && change.IsSuccess && detach.IsSuccess && hide.IsSuccess, string.Join(" | ", new[] { attach.IsError ? attach.Error.ToString() : "", change.IsError ? change.Error.ToString() : "", detach.IsError ? detach.Error.ToString() : "", hide.IsError ? hide.Error.ToString() : "" }));
        Assert.Equal(4, _h.Slice.Store.Events.Count);
        Assert.All(_h.Slice.Store.Events, recorded => Assert.StartsWith("session:", recorded.Envelope.Actor, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task A_session_may_attach_or_change_a_project_with_notes(string caller)
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var entered = Enter(caller);

        var attach = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Notes: "plain notes"), Token);
        var change = await _h.Change().Handle(new ChangeProjectRequest(ProjectRows.HandMadeId.Value, 1, new ProjectEdit(Notes: "more plain notes")), Token);

        Assert.True(attach.IsSuccess && change.IsSuccess);
        Assert.All(_h.Slice.Store.Events, recorded => Assert.StartsWith("session:", recorded.Envelope.Actor, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_call_with_no_caller_is_still_refused()
    {
        await _h.Lease();

        var attach = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen"), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, attach.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }
}
