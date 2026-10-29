using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Removing a project (projects page spec §5.1, §5.3): 'project.detached' at the version the owner saw.</summary>
[Lane(TestLane.Unit)]
public sealed class DetachProjectTests
{
    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_project_is_detached_at_the_version_the_owner_saw()
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var human = _h.Human();

        var result = await _h.Detach().Handle(new DetachProjectRequest(ProjectRows.HandMadeId, 1), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<ProjectDetached>(recorded);
        Expect.Equal(EventTypes.ProjectDetached, recorded.Envelope.Type);
        Expect.Equal($"project.detached:{ProjectRows.HandMadeId.Value}:v2", recorded.IdempotencyKey);
        Expect.Equal((ProjectRows.HandMadeId, 2L), (payload.Id, payload.Version));
        Expect.Equal((recorded.Sequence, ProjectRows.HandMadeId.Value, 2L), (result.Value.Accepted.Sequence, result.Value.Project, result.Value.Version));
    }

    [Fact]
    public async Task A_stale_version_is_SND_PROJECT_409()
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var human = _h.Human();

        var result = await _h.Detach().Handle(new DetachProjectRequest(ProjectRows.HandMadeId, 3), Token);

        Expect.Equal(SindriErrors.ProjectConflict.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task An_unknown_project_is_SND_PROJECT_404()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Detach().Handle(new DetachProjectRequest(ProjectRows.HandMadeId, 1), Token);

        Expect.Equal(SindriErrors.ProjectNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Without_a_lease_it_is_not_running()
    {
        using var human = _h.Human();

        var result = await _h.Detach().Handle(new DetachProjectRequest(ProjectRows.HandMadeId, 1), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }
}
