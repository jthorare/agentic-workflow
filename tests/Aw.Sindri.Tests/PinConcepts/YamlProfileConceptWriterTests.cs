using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>The profile edit PinConcepts makes on the live file (concept pins spec §3.6, "The write"): only 'index.concepts' changes, every other key and comment stays, and the write is versioned.</summary>
[Lane(TestLane.Integration)]
public sealed class YamlProfileConceptWriterTests : IDisposable
{
    private const string Head = "# my profile\nschemaVersion: 1\nuser: thor   # me\nrepos: []\n";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-concept-writer-" + RealTime.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ProfilePath => Path.Combine(_dir, "profile.yaml");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private YamlProfileConceptWriter Writer() => new(new SindriSettings(_dir, ProfilePath), ProfileFiles.Options);

    private void Put(string text)
    {
        _ = Directory.CreateDirectory(_dir);
        File.WriteAllText(ProfilePath, text);
    }

    private LoadedProfile Load()
    {
        Assert.Null(YamlToJson.TryConvert(File.ReadAllText(ProfilePath), out var json));
        var loaded = ProfileReading.FromJson(json, ProfilePath, ProfileFiles.Options);
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
        return loaded.Value;
    }

    private IEnumerable<string> Ids() => (Load().Profile.Index?.Concepts ?? []).Select(static c => c.Id?.Value).OfType<string>();

    private static ConceptEdit Set(string id, string name, string? summary = null) => new(ConceptPinId.From(id), new ConceptSettings(Name: name, Summary: summary));

    private static ConceptEdit Remove(string id) => new(ConceptPinId.From(id), Entry: null);

    private string Text() => File.ReadAllText(ProfilePath);

    private const string Block = "index:\n  maxFileKB: 100\n  concepts:\n    # the money one\n    - id: billing\n      # note inside billing\n      name: Billing\n      aliases:\n        - payments\n    - id: scheduling\n      name: Scheduling   # shifts\n    # trailing note\n  maxTotalMB: 200\nservices:\n  heartbeat: { every: 30s }\n";

    [Fact]
    public async Task A_new_entry_goes_after_the_last_one_in_an_existing_block()
    {
        Put(Head + Block);

        var done = await Writer().Write([Set("tabs", "Tabs")], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        Assert.Equal(["billing", "scheduling", "tabs"], Ids(), StringComparer.Ordinal);
        Assert.True(Text().IndexOf("id: \"tabs\"", StringComparison.Ordinal) < Text().IndexOf("maxTotalMB", StringComparison.Ordinal), "the entry sits in the concepts block");
        Assert.True(Text().IndexOf("# trailing note", StringComparison.Ordinal) > Text().IndexOf("id: \"tabs\"", StringComparison.Ordinal), "a comment after the last entry stays after it");
    }

    [Fact]
    public async Task An_empty_flow_list_becomes_a_block()
    {
        Put(Head + "index:\n  concepts: []\n");

        _ = await Writer().Write([Set("billing", "Billing")], ifHash: null, Token);

        Assert.Equal(["billing"], Ids(), StringComparer.Ordinal);
        Assert.DoesNotContain("[]", Text().Replace("repos: []", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_index_with_no_concepts_gets_them_as_its_last_key()
    {
        Put(Head + "index:\n  maxFileKB: 100\nservices:\n  heartbeat: { every: 30s }\n");

        _ = await Writer().Write([Set("billing", "Billing")], ifHash: null, Token);

        Assert.Equal(["billing"], Ids(), StringComparer.Ordinal);
        Assert.Equal(100, Load().Profile.Index!.MaxFileKB);
        Assert.True(Text().IndexOf("concepts:", StringComparison.Ordinal) < Text().IndexOf("services:", StringComparison.Ordinal), "concepts sits inside index");
        Assert.Contains("services:\n  heartbeat: { every: 30s }", Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_profile_with_no_index_gets_one_at_the_end()
    {
        Put(Head + "services:\n  heartbeat: { every: 30s }\n");

        _ = await Writer().Write([Set("billing", "Billing")], ifHash: null, Token);

        Assert.Equal(["billing"], Ids(), StringComparer.Ordinal);
        Assert.True(Load().Profile.Services!.ContainsKey("heartbeat"));
        Assert.StartsWith(Head + "services:\n  heartbeat: { every: 30s }\nindex:\n  concepts:\n", Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_replaced_entry_keeps_its_position_and_everything_around_it()
    {
        Put(Head + Block);

        var done = await Writer().Write([Set("billing", "Payments", "Takes the money")], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        Assert.Equal(["billing", "scheduling"], Ids(), StringComparer.Ordinal);
        var concepts = Load().Profile.Index!.Concepts!;
        Assert.Equal("Payments", concepts[0].Name);
        Assert.Equal("Takes the money", concepts[0].Summary);
        Assert.Null(concepts[0].Aliases);
        var text = Text();
        Assert.Contains("# my profile", text, StringComparison.Ordinal);
        Assert.Contains("user: thor   # me", text, StringComparison.Ordinal);
        Assert.Contains("maxFileKB: 100", text, StringComparison.Ordinal);
        Assert.Contains("maxTotalMB: 200", text, StringComparison.Ordinal);
        Assert.Contains("    # the money one\n    - id: \"billing\"", text, StringComparison.Ordinal);   // the comment above the entry stays
        Assert.DoesNotContain("# note inside billing", text, StringComparison.Ordinal);   // a comment inside it goes with it
        Assert.Contains("      name: Scheduling   # shifts", text, StringComparison.Ordinal);
        Assert.Contains("    # trailing note", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Comments_in_other_entries_and_above_the_block_survive_an_insert_a_replace_and_a_remove()
    {
        Put(Head + "index:\n  # pins we keep\n  concepts:\n    - id: billing\n      name: Billing\n    - id: scheduling\n      # why scheduling\n      name: Scheduling\n");

        _ = await Writer().Write([Set("tabs", "Tabs")], ifHash: null, Token);
        _ = await Writer().Write([Set("billing", "Payments")], ifHash: null, Token);
        _ = await Writer().Write([Remove("tabs")], ifHash: null, Token);

        Assert.Equal(["billing", "scheduling"], Ids(), StringComparer.Ordinal);
        Assert.Contains("  # pins we keep\n  concepts:", Text(), StringComparison.Ordinal);
        Assert.Contains("      # why scheduling\n      name: Scheduling", Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_removed_entry_takes_only_its_own_lines()
    {
        Put(Head + Block);

        _ = await Writer().Write([Remove("billing")], ifHash: null, Token);

        Assert.Equal(["scheduling"], Ids(), StringComparer.Ordinal);
        var text = Text();
        Assert.DoesNotContain("# note inside billing", text, StringComparison.Ordinal);
        Assert.DoesNotContain("payments", text, StringComparison.Ordinal);
        Assert.Contains("    # the money one\n    - id: scheduling", text, StringComparison.Ordinal);
        Assert.Contains("maxFileKB: 100", text, StringComparison.Ordinal);
        Assert.Contains("maxTotalMB: 200", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removing_the_last_entry_leaves_an_empty_list()
    {
        Put(Head + "index:\n  concepts:\n    - id: billing\n      name: Billing\n");

        var done = await Writer().Write([Remove("billing")], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        Assert.Empty(Ids());
    }

    [Fact]
    public async Task An_entry_without_an_id_is_left_alone()
    {
        Put(Head + "index:\n  concepts:\n    - name: Old\n    - id: billing\n      name: Billing\n");

        _ = await Writer().Write([Set("old", "Newer"), Remove("billing")], ifHash: null, Token);

        Assert.Equal(["old"], Ids(), StringComparer.Ordinal);
        Assert.Equal(["Old", "Newer"], Load().Profile.Index!.Concepts!.Select(static c => c.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_written_entry_has_an_id_and_reads_back_with_every_field()
    {
        Put(Head + "index:\n  concepts:\n    - id: scheduling\n      name: Scheduling\n");
        var entry = new ConceptSettings(
            Name: "Bill \"A\" \\ ing",
            Summary: "true",
            Aliases: ["payments", "123"],
            Features: ["feature:shifts", "feature:shifts.*"],
            Paths: ["src/billing/**"],
            DependsOn: ["scheduling"],
            Owns: ["invoices"]);

        var done = await Writer().Write([new ConceptEdit(ConceptPinId.From("billing"), entry)], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        Assert.Contains("- id: \"billing\"", Text(), StringComparison.Ordinal);
        var read = Load().Profile.Index!.Concepts!.Single(static c => c.Id!.Value.Value == "billing");
        Assert.Equal(entry.Name, read.Name);
        Assert.Equal(entry.Summary, read.Summary);
        Assert.Equal(entry.Aliases, read.Aliases);
        Assert.Equal(entry.Features, read.Features);
        Assert.Equal(entry.Paths, read.Paths);
        Assert.Equal(entry.DependsOn, read.DependsOn);
        Assert.Equal(entry.Owns, read.Owns);
    }

    [Fact]
    public async Task An_empty_list_reads_back_empty_and_a_null_one_stays_null()
    {
        Put(Head);

        _ = await Writer().Write([new ConceptEdit(ConceptPinId.From("billing"), new ConceptSettings(Name: "Billing", Aliases: []))], ifHash: null, Token);

        var read = Load().Profile.Index!.Concepts!.Single();
        Assert.Empty(read.Aliases!);
        Assert.Null(read.Features);
        Assert.Null(read.Summary);
    }

    [Fact]
    public async Task A_hash_that_is_not_the_files_is_SND_PROFILE_003_and_the_file_is_not_touched()
    {
        Put(Head + Block);
        var before = Text();
        var stale = Load().Hash;
        Put(before.Replace("user: thor", "user: other", StringComparison.Ordinal));   // the profile changed after the caller read it
        var bytes = await File.ReadAllBytesAsync(ProfilePath, Token);

        var done = await Writer().Write([Set("tabs", "Tabs")], stale, Token);

        Assert.Equal("SND-PROFILE-003", done.Error.Code);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(ProfilePath, Token));
    }

    [Fact]
    public async Task A_matching_hash_writes_and_the_returned_hash_is_the_new_files()
    {
        Put(Head + Block);
        var hash = Load().Hash;

        var done = await Writer().Write([Set("tabs", "Tabs")], hash, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        Assert.Equal(Load().Hash, done.Value);
        Assert.NotEqual(hash, done.Value);
    }

    [Fact]
    public async Task An_entry_the_profile_rules_reject_is_returned_and_nothing_is_written()
    {
        Put(Head + Block);
        var bytes = await File.ReadAllBytesAsync(ProfilePath, Token);

        var done = await Writer().Write([new ConceptEdit(ConceptPinId.From("tabs"), new ConceptSettings(Name: "Tabs", DependsOn: ["nobody"]))], ifHash: null, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("dependsOn", done.Error.Message, StringComparison.Ordinal);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(ProfilePath, Token));
    }

    [Fact]
    public async Task A_missing_profile_is_SND_PROFILE_001()
    {
        var done = await Writer().Write([Set("tabs", "Tabs")], ifHash: null, Token);

        Assert.Equal("SND-PROFILE-001", done.Error.Code);
    }

    [Fact]
    public async Task A_file_that_changes_once_between_the_read_and_the_write_is_read_again_and_lands()
    {
        Put(Head + Block);
        var writer = new YamlProfileConceptWriter(new SindriSettings(_dir, ProfilePath), ProfileFiles.Options) { BeforeWrite = static (path, attempt) => { if (attempt == 0) { File.AppendAllText(path, "# changed\n"); } } };

        var done = await writer.Write([Set("tabs", "Tabs")], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        Assert.Contains("# changed", Text(), StringComparison.Ordinal);
        Assert.Equal(["billing", "scheduling", "tabs"], Ids(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_file_that_changes_twice_is_SND_PROFILE_002_and_the_entry_is_not_written()
    {
        Put(Head + Block);
        var writer = new YamlProfileConceptWriter(new SindriSettings(_dir, ProfilePath), ProfileFiles.Options) { BeforeWrite = static (path, attempt) => File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture, $"# changed {attempt}\n")) };

        var done = await writer.Write([Set("tabs", "Tabs")], ifHash: null, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("Try again.", done.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("tabs", Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_flow_style_index_is_refused_with_the_lines_to_add_and_nothing_is_written()
    {
        var before = Head + "index: { concepts: [ { id: billing, name: Billing } ] }\n";
        Put(before);

        var done = await Writer().Write([Set("tabs", "Tabs")], ifHash: null, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("won't edit", done.Error.Message, StringComparison.Ordinal);
        Assert.Contains("tabs", done.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, Text());
    }

    [Fact]
    public async Task A_block_with_an_anchor_is_refused_and_nothing_is_written()
    {
        var before = Head + "index:\n  concepts:\n    - &first\n      id: billing\n      name: Billing\n";
        Put(before);

        var done = await Writer().Write([Set("tabs", "Tabs")], ifHash: null, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Equal(before, Text());
    }

    [Fact]
    public async Task A_file_with_windows_line_endings_keeps_them()
    {
        Put((Head + Block).Replace("\n", "\r\n", StringComparison.Ordinal));

        _ = await Writer().Write([Set("tabs", "Tabs"), Remove("scheduling")], ifHash: null, Token);

        var text = Text();
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.StartsWith("# my profile\r\nschemaVersion: 1\r\n", text, StringComparison.Ordinal);
        Assert.Equal(["billing", "tabs"], Ids(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_entry_without_an_id_is_replaced_by_its_name_and_gets_one()
    {
        Put(Head + "index:\n  concepts:\n    - name: Billing   # hand written\n      summary: Old\n    - id: auth\n      name: Auth\n");

        var done = await Writer().Write([new ConceptEdit(ConceptPinId.From("billing"), new ConceptSettings(Name: "Billing", Summary: "New"), MatchName: "Billing")], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        Assert.Equal(["billing", "auth"], Ids(), StringComparer.Ordinal);
        Assert.Equal("New", Load().Profile.Index!.Concepts![0].Summary);
        Assert.DoesNotContain("hand written", Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_id_match_wins_over_a_name_match()
    {
        Put(Head + "index:\n  concepts:\n    - name: Billing\n    - id: billing\n      name: Money\n");

        _ = await Writer().Write([new ConceptEdit(ConceptPinId.From("billing"), new ConceptSettings(Name: "Money", Summary: "S"), MatchName: "Billing")], ifHash: null, Token);

        Assert.Equal(["billing"], Ids(), StringComparer.Ordinal);
        Assert.Equal(2, Load().Profile.Index!.Concepts!.Count);
        Assert.Equal("S", Load().Profile.Index!.Concepts![1].Summary);
    }
}
