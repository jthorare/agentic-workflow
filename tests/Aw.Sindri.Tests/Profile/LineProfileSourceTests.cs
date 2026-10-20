using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The line's profile keys through the YAML profile file, and the box cap they are held to (line spec LN-WF9).</summary>
public sealed class LineProfileSourceTests : IDisposable
{
    private const string Yaml =
        "schemaVersion: 1\n" +
        "user: thor\n" +
        "workflow:\n" +
        "  packs:\n" +
        "    style: { default: formal, repos: { web: plain } }\n" +
        "    lenses:\n" +
        "      design: { default: design-two }\n" +
        "      review: { default: code-review }\n" +
        "    pins:\n" +
        "      style/formal: \"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n" +
        "  line: { parallel: 3, maxOpenPrs: 6, maxStack: 3 }\n" +
        "  plan:\n" +
        "    maxPhaseSize: S\n" +
        "    sizes: { S: { files: 10, items: 5, budget: { tokens: 500000, turns: 70, wallMinutes: 50 } } }\n" +
        "  prove: { holdoutMin: 3 }\n" +
        "  build: { commands: [\"dotnet build -warnaserror\", \"dotnet test\"] }\n" +
        "  ship: { draft: true }\n" +
        "  design: { models: { writer: claude-writer, lens: codex-lens } }\n" +
        "  review: { models: { lens: codex-lens } }\n" +
        "  steps: { longMax: 3h }\n" +
        "  docs: { globs: [\"docs/**\"] }\n";

    private readonly string _state = Directory.CreateTempSubdirectory("aw-sindri-cap-").FullName;

    public void Dispose() => Directory.Delete(_state, recursive: true);

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_every_line_key()
    {
        using var files = new ProfileFiles();
        files.Write(Yaml);

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.True(loaded.IsSuccess, loaded.IsError ? loaded.Error.Message : string.Empty);
        var profile = loaded.Value.Profile;
        Assert.Equal((3, 6, 3), (LineProfile.Parallel(profile), LineProfile.MaxOpenPrs(profile), LineProfile.MaxStack(profile)));
        Assert.Equal(("S", 3, "3h", true), (LineProfile.MaxPhaseSize(profile), LineProfile.HoldoutMin(profile), LineProfile.LongMax(profile), LineProfile.Draft(profile)));
        Assert.Equal("formal", profile.Workflow!.Packs!.Style!.Default);
        Assert.Equal("plain", profile.Workflow.Packs.Style.Repos!["web"]);
        Assert.Equal("claude-writer", profile.Workflow.Design!.Models!.Writer);
        Assert.Equal(["dotnet build -warnaserror", "dotnet test"], profile.Workflow.Build!.Commands, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_line_key_is_refused()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  line:\n    speed: 9\n");

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ProfileInvalid.Code, loaded.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_source_over_the_box_cap_refuses_a_parallel_above_it_and_reads_it_at_the_cap()
    {
        using var files = new ProfileFiles();
        files.Write(Yaml);
        var capped = new YamlProfileSource(files.Settings, ProfileFiles.Options, () => new BoxLimits(2));
        var raised = new YamlProfileSource(files.Settings, ProfileFiles.Options, () => new BoxLimits(3));

        var refused = await capped.Load(TestContext.Current.CancellationToken);
        var accepted = await raised.Load(TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ProfileInvalid.Code, refused.Error.Code);
        Assert.Contains("workflow.line.parallel", refused.Error.Message, StringComparison.Ordinal);
        Assert.True(accepted.IsSuccess);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_cap_is_read_each_load_so_a_later_drop_is_seen()
    {
        using var files = new ProfileFiles();
        files.Write(Yaml);
        var cap = 3;
        var source = new YamlProfileSource(files.Settings, ProfileFiles.Options, () => new BoxLimits(cap));

        Assert.True((await source.Load(TestContext.Current.CancellationToken)).IsSuccess);
        cap = 1;
        Assert.True((await source.Load(TestContext.Current.CancellationToken)).IsError);
    }

    private static string? Env(string name, string? slots) => name == "AW_HEAVY_SLOTS" ? slots : null;

    [Fact, Trait("Lane", "integration")]
    public async Task The_cap_file_wins_over_AW_HEAVY_SLOTS()
    {
        await File.WriteAllTextAsync(Path.Combine(_state, "heavy-slots-cap"), "3\n", TestContext.Current.CancellationToken);

        Assert.Equal(3, HeavyCap.Read(n => Env(n, "1"), _state).HeavySlots);
    }

    [Fact, Trait("Lane", "integration")]
    public void Without_a_cap_file_it_is_AW_HEAVY_SLOTS_and_without_that_one_slot()
    {
        Assert.Equal(4, HeavyCap.Read(n => Env(n, "4"), _state).HeavySlots);
        Assert.Equal(1, HeavyCap.Read(n => Env(n, slots: null), _state).HeavySlots);
        Assert.Equal(1, HeavyCap.Read(n => Env(n, "two"), _state).HeavySlots);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("many")]
    [InlineData("")]
    public async Task A_cap_file_that_is_not_a_whole_number_of_one_or_more_is_ignored(string content)
    {
        await File.WriteAllTextAsync(Path.Combine(_state, "heavy-slots-cap"), content, TestContext.Current.CancellationToken);

        Assert.Equal(2, HeavyCap.Read(n => Env(n, "2"), _state).HeavySlots);
    }
}
