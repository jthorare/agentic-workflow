using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Hiding and unhiding a led project (projects page spec §5.1, §5.3): 'project.hidden' at the hide version the owner saw (0 before the first).</summary>
[Lane(TestLane.Unit)]
public sealed class HideProjectTests
{
    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_project_never_hidden_is_hidden_at_version_zero()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Hide().Handle(new HideProjectRequest(ProjectRows.LinearKey, Hidden: true, Version: 0), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<ProjectHidden>(recorded);
        Expect.Equal($"project.hidden:{ProjectRows.LinearKey}:v1", recorded.IdempotencyKey);
        Expect.Equal((ProjectRows.LinearKey, true, 1L), (payload.Linear, payload.Hidden, payload.Version));
        Expect.Equal((recorded.Sequence, ProjectRows.LinearKey, 1L), (result.Value.Accepted.Sequence, result.Value.Project, result.Value.Version));
    }

    [Fact]
    public async Task A_hidden_project_is_unhidden_at_its_hide_version()
    {
        await _h.Lease();
        await _h.SeedHidden(version: 1, hidden: true);
        using var human = _h.Human();

        var result = await _h.Hide().Handle(new HideProjectRequest(ProjectRows.LinearKey, Hidden: false, Version: 1), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var payload = _h.Payload<ProjectHidden>(Assert.Single(_h.Slice.Store.Events));
        Expect.Equal((false, 2L), (payload.Hidden, payload.Version));
    }

    [Fact]
    public async Task A_stale_hide_version_is_SND_PROJECT_409()
    {
        await _h.Lease();
        await _h.SeedHidden(version: 1, hidden: true);
        using var human = _h.Human();

        var result = await _h.Hide().Handle(new HideProjectRequest(ProjectRows.LinearKey, Hidden: false, Version: 0), Token);

        Expect.Equal(SindriErrors.ProjectConflict.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_bad_key_is_SND_PROJECT_004_naming_the_field()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Hide().Handle(new HideProjectRequest("banana", Hidden: true, Version: 0), Token);

        Expect.Equal(SindriErrors.ProjectInvalid.Code, result.Error.Code);
        Assert.StartsWith("linear:", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Losing_the_version_key_race_is_SND_PROJECT_409()
    {
        await _h.Lease();
        var rival = new ProjectHidden(_h.Slice.Ids.New<CommandId>(), ProjectRows.LinearKey, Hidden: true, Version: 1);
        _ = await _h.Slice.Store.Append(_h.Slice.Events.Entry(EventTypes.ProjectHidden, rival, $"project.hidden:{ProjectRows.LinearKey}:v1"), _h.Slice.Held.Current!, Token);
        using var human = _h.Human();

        var result = await _h.Hide().Handle(new HideProjectRequest(ProjectRows.LinearKey, Hidden: true, Version: 0), Token);

        Expect.Equal(SindriErrors.ProjectConflict.Code, result.Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Without_a_lease_it_is_not_running()
    {
        using var human = _h.Human();

        var result = await _h.Hide().Handle(new HideProjectRequest(ProjectRows.LinearKey, Hidden: true, Version: 0), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }
}
