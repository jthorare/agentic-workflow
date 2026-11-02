using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Features.ProjectFacts;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Issue #1739: a profile that lists Linear teams but names no token never syncs Linear, so the Projects page and the doctor's Linear row both say Linear needs a token, in the same words.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearTokenMissingTests
{
    private const string NeedsToken = "Linear needs a token";
    private const string TeamsOnly = """{"sources":{"linear":{"teams":["ENG"]}}}""";
    private const string NoLinear = """{}""";

    private static readonly RepoEntry[] _repos = [new RepoEntry("split-panes", "/repos/split-panes")];

    private readonly ProjectWriteHarness _h = new();
    private readonly FakeProjectFacts _facts = new FakeProjectFacts().With(new(Owner: null, [], [], ["ENG"], FirstReadDone: false));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_projects_page_says_linear_needs_a_token_when_teams_are_listed_with_no_token()
    {
        var empty = await PageSentence(TeamsOnly);

        Assert.NotNull(empty);
        Assert.StartsWith(NeedsToken, empty, StringComparison.Ordinal);
        Assert.Contains("sindri index connect linear", empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_profile_with_no_linear_entry_keeps_the_connect_sentence()
    {
        var empty = await PageSentence(NoLinear);

        Assert.Equal("No projects yet. Connect Linear or add one by hand.", empty);
    }

    [Fact]
    public async Task The_doctor_linear_row_warns_in_the_same_words_and_names_the_connect_verb()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))).Check(Loaded(TeamsOnly), Token);

        Assert.Equal(CheckState.Warn, check.State);
        Assert.StartsWith(NeedsToken, check.Detail, StringComparison.Ordinal);
        Assert.Equal("sindri index connect linear", check.Fix);
    }

    private async Task<string?> PageSentence(string context)
    {
        _ = await _h.Slice.Profiles.Save(Loaded(context), Token);
        using var entered = _h.Human();
        var reader = new ProjectPageReader(_h.Views.Views, _facts, _h.Slice.Profiles, FakeContextEngine.Instance, _h.Slice.Clock);

        var result = await new ListProjectsService(reader, _h.Views.Progress, _h.Slice.Callers).Handle(new ListProjectsRequest(All: false), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value.Empty;
    }

    private static LoadedProfile Loaded(string context)
    {
        var profile = new SindriProfile(1, "thor", Repos: _repos, Context: JsonDocument.Parse(context).RootElement.Clone());
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }
}
