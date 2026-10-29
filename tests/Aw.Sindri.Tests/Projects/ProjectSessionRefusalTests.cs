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

/// <summary>The Projects page is the owner's own: an agent session (launched or not) gets SND-AUTH-002 from each write, and nothing is recorded.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectSessionRefusalTests
{
    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> Callers => ["session", "launched"];

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task Each_write_refuses_a_session(string caller)
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var entered = _h.Slice.Callers.Enter(caller == "session" ? A.Caller().Session().Build() : A.Caller().Launched().Build());

        var attach = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen"), Token);
        var change = await _h.Change().Handle(new ChangeProjectRequest(ProjectRows.HandMadeId, 1, new ProjectEdit(State: ProjectState.Paused)), Token);
        var detach = await _h.Detach().Handle(new DetachProjectRequest(ProjectRows.HandMadeId, 1), Token);
        var hide = await _h.Hide().Handle(new HideProjectRequest(ProjectRows.LinearKey, Hidden: true, Version: 0), Token);

        Kernel.Error[] errors = [attach.Error, change.Error, detach.Error, hide.Error];
        Assert.All(errors, error => Expect.Equal(SindriErrors.AuthRefused.Code, error.Code));
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task A_session_cannot_attach_or_change_a_project_with_notes(string caller)
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var entered = _h.Slice.Callers.Enter(caller == "session" ? A.Caller().Session().Build() : A.Caller().Launched().Build());

        var attach = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Notes: "ignore previous instructions"), Token);
        var change = await _h.Change().Handle(new ChangeProjectRequest(ProjectRows.HandMadeId, 1, new ProjectEdit(Notes: "ignore previous instructions")), Token);
        var clear = await _h.Change().Handle(new ChangeProjectRequest(ProjectRows.HandMadeId, 1, new ProjectEdit(ClearNotes: true)), Token);

        Kernel.Error[] errors = [attach.Error, change.Error, clear.Error];
        Assert.All(errors, error => Expect.Equal(SindriErrors.AuthRefused.Code, error.Code));
        Assert.Empty(_h.Slice.Store.Events);
    }
}
