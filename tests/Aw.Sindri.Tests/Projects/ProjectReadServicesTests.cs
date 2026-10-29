using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Features.ProjectFacts;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>ListProjects and GetProject (projects page spec §6.2): the plumbing around ProjectCards.Build: the caller, the attached keys asked of Context, and how a project is named.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectReadServicesTests
{
    private const string Unknown = "prj_01J9Z3K5M7Q2R4T6V8WXYZ9999";

    private readonly ProjectWriteHarness _h = new();
    private readonly FakeProjectFacts _facts = new();

    private ProjectPageReader Reader() => new(_h.Views.Views, _facts, _h.Slice.Profiles, FakeContextEngine.Instance, _h.Slice.Clock);

    private ListProjectsService List() => new(Reader(), _h.Views.Progress, _h.Slice.Callers);

    private GetProjectService Get() => new(Reader(), _h.Views.Views, _h.Views.Progress, _h.Slice.Callers);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListProjects_without_a_caller_is_refused()
    {
        var result = await List().Handle(new ListProjectsRequest(All: false), Token);

        Assert.True(result.IsError);
        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Empty(_facts.Asked);
    }

    [Fact]
    public async Task GetProject_without_a_caller_is_refused()
    {
        var result = await Get().Handle(new GetProjectRequest(ProjectRows.HandMadeId.Value), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Fact]
    public async Task ListProjects_asks_Context_for_the_attached_linear_keys_only()
    {
        await _h.SeedHandMade();
        await _h.SeedAttached();
        using var entered = _h.Human();

        var result = await List().Handle(new ListProjectsRequest(All: false), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence([ProjectRows.LinearKey], [.. _facts.Asked.Single()]);
        Expect.Equal(_h.Views.Progress.Current.AsOf, result.Value.AsOf);
    }

    [Fact]
    public async Task GetProject_names_a_hand_made_project_by_its_id()
    {
        await _h.SeedHandMade();
        using var entered = _h.Human();

        var result = await Get().Handle(new GetProjectRequest(ProjectRows.HandMadeId.Value), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(ProjectRows.HandMadeId.Value, result.Value.Project.Key);
        Assert.False(result.Value.Hidden);
    }

    [Fact]
    public async Task GetProject_names_an_attached_project_by_its_linear_key()
    {
        await _h.SeedAttached();
        using var entered = _h.Human();

        var result = await Get().Handle(new GetProjectRequest(ProjectRows.LinearKey), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(ProjectRows.LinearId.Value, result.Value.Project.Key);
    }

    [Fact]
    public async Task GetProject_with_an_id_nothing_holds_is_SND_PROJECT_404()
    {
        await _h.SeedHandMade();
        using var entered = _h.Human();

        var result = await Get().Handle(new GetProjectRequest(Unknown), Token);

        Expect.Equal(SindriErrors.ProjectNotFound.Code, result.Error.Code);
        Assert.Contains("sindri projects", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProject_with_text_that_is_not_a_project_reference_is_SND_PROJECT_404()
    {
        using var entered = _h.Human();

        var result = await Get().Handle(new GetProjectRequest("not a project"), Token);

        Expect.Equal(SindriErrors.ProjectNotFound.Code, result.Error.Code);
    }
}
