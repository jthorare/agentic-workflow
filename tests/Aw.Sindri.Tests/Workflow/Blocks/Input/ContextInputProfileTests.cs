using System.Text.Json;
using Aw.Context.Core.History;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>A Step input's '{ profile: key }' argument is resolved inside the fill against the approved profile (#399); a key no input can read fails the fill with SND-WF-021, and no approved profile is an unavailable section (SND-PROFILE-005).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextInputProfileTests
{
    private const string Key = "workflow.scoping.historyWindow";
    private static readonly string[] _anchors = ["feature:a"];

    private static LoadedProfile WithWindow(HistoryWindowSettings? window) =>
        ContextInputFixtures.Profile() with { Profile = ContextInputFixtures.Profile().Profile with { Workflow = new WorkflowSettings(Scoping: window is null ? null : new ScopingSettings(window)) } };

    private static ContextHistoryInput Input(ContextHistoryInputTests.StubHistory history, LoadedProfile? profile) =>
        new(EngineProxy.Of(history), new ContextInputScopes(new ContextInputFixtures.Profiles(ContextInputFixtures.Profile())),
            new ContextInputFacts(new ContextInputFixtures.MemoryFacts(), new ContextInputFixtures.Profiles(profile)), ShippedComposition.Json);

    private static StepInputContext Context() => new(
        BlockId.From(ContextHistoryInput.Id), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["anchors"] = JsonSerializer.SerializeToElement(_anchors),
            ["window"] = JsonDocument.Parse("{\"profile\":\"" + Key + "\"}").RootElement.Clone(),
        },
        new Dictionary<FactKey, JsonElement>(), ContextInputFixtures.Web);

    [Fact]
    public async Task The_profiles_window_is_what_the_history_is_read_with()
    {
        var history = new ContextHistoryInputTests.StubHistory(() => ContextHistoryTextTests.View());

        var filled = await Input(history, WithWindow(new HistoryWindowSettings(Since: "2026-01-01"))).Fill(Context(), TestContext.Current.CancellationToken);

        Assert.False(filled.IsError);
        var (_, window, _) = Assert.Single(history.Calls);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), window.Since);
    }

    [Fact]
    public async Task An_empty_window_in_the_profile_is_the_open_window()
    {
        var history = new ContextHistoryInputTests.StubHistory(() => ContextHistoryTextTests.View());

        _ = await Input(history, WithWindow(new HistoryWindowSettings())).Fill(Context(), TestContext.Current.CancellationToken);

        var (_, window, _) = Assert.Single(history.Calls);
        Assert.Equal(HistoryWindow.Open, window);
    }

    [Fact]
    public async Task A_profile_without_the_key_reads_the_open_window()
    {
        var history = new ContextHistoryInputTests.StubHistory(() => ContextHistoryTextTests.View());

        var filled = await Input(history, WithWindow(window: null)).Fill(Context(), TestContext.Current.CancellationToken);

        Assert.False(filled.IsError);
        var (_, window, _) = Assert.Single(history.Calls);
        Assert.Equal(HistoryWindow.Open, window);
    }

    [Fact]
    public async Task A_key_no_input_can_read_fails_the_fill_with_SND_WF_021()
    {
        var history = new ContextHistoryInputTests.StubHistory(() => ContextHistoryTextTests.View());
        var other = Context() with { Arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["anchors"] = JsonSerializer.SerializeToElement(_anchors), ["window"] = JsonDocument.Parse("{\"profile\":\"workflow.nope\"}").RootElement.Clone() } };

        var filled = await Input(history, WithWindow(window: null)).Fill(other, TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowErrors.ProfileKeyMissing.Code, filled.Error.Code);
        Assert.Contains("workflow.nope", filled.Error.Message, StringComparison.Ordinal);
        Assert.Empty(history.Calls);
    }

    [Fact]
    public async Task No_approved_profile_is_an_unavailable_section_naming_SND_PROFILE_005()
    {
        var history = new ContextHistoryInputTests.StubHistory(() => ContextHistoryTextTests.View());

        var filled = await Input(history, profile: null).Fill(Context(), TestContext.Current.CancellationToken);

        Assert.False(filled.IsError);
        Assert.Contains("SND-PROFILE-005", filled.Value.Sections["history"], StringComparison.Ordinal);
        Assert.Empty(history.Calls);
    }
}
