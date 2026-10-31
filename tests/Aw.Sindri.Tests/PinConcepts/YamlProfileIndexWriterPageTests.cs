using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>The page half of the index writer (pages spec §3.1, "The writer"): a page lands in the top-level 'index.pages' or in its repo's own, every other key and comment stays, and the write is versioned like a concept's.</summary>
[Lane(TestLane.Integration)]
public sealed class YamlProfileIndexWriterPageTests : IDisposable
{
    private const string Profile =
        "# my profile\nschemaVersion: 1\nuser: thor   # me\n" +
        "index:\n  maxFileKB: 100\n  concepts:\n    # the money one\n    - id: billing\n      name: Billing\n  pages:\n    - id: settings   # by hand\n      name: Settings\n" +
        "repos:\n  - name: web\n    path: /w   # the app\n    index:\n      maxFileKB: 50\n  - name: api\n    path: /a\n" +
        "services:\n  heartbeat: { every: 30s }\n";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-index-writer-pages-" + RealTime.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ProfilePath => Path.Combine(_dir, "profile.yaml");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private YamlProfileIndexWriter Writer() => new(new SindriSettings(_dir, ProfilePath), ProfileFiles.Options);

    private void Put(string text)
    {
        _ = Directory.CreateDirectory(_dir);
        File.WriteAllText(ProfilePath, text);
    }

    private string Text() => File.ReadAllText(ProfilePath);

    private SindriProfile Load()
    {
        Assert.Null(YamlToJson.TryConvert(Text(), out var json));
        var loaded = ProfileReading.FromJson(json, ProfilePath, ProfileFiles.Options);
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
        return loaded.Value.Profile;
    }

    private static PageSettings Week => new(
        Name: "Schedule week",
        Surface: "surface:route/GET:/schedule/week",
        Actions: [new PageActionSettings(Id: PageActionPinId.From("submit"), Name: "Submit schedule", Action: "action:SchedulePage.submit-schedule", Concepts: ["billing", "concept:approval-workflow"])]);

    private static PageEdit Set(string id, PageSettings page, string? repo = null) => new(PagePinId.From(id), page, repo);

    private static IEnumerable<string?> PageIds(IReadOnlyList<PageSettings>? pages) => (pages ?? []).Select(static p => p.Id?.Value);

    [Fact]
    public async Task Writing_a_page_keeps_every_other_key_and_comment()
    {
        Put(Profile);

        var done = await Writer().Write([Set("schedule-week", Week)], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        var written = Text();
        foreach (var kept in new[] { "# my profile", "user: thor   # me", "    # the money one", "    - id: billing", "    - id: settings   # by hand", "    path: /w   # the app", "      maxFileKB: 50", "  heartbeat: { every: 30s }" })
        {
            Assert.Contains(kept, written, StringComparison.Ordinal);
        }

        var profile = Load();
        Assert.Equal(["settings", "schedule-week"], PageIds(profile.Index!.Pages), StringComparer.Ordinal);
        Assert.Equal(["billing"], profile.Index.Concepts!.Select(static c => c.Id?.Value), StringComparer.Ordinal);
        Assert.Equal(Profile, written.Replace(string.Join('\n', ProfileIndexText.Entry("schedule-week", Week, 4)) + "\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_written_page_reads_back_with_every_field()
    {
        Put(Profile);

        _ = await Writer().Write([Set("schedule-week", Week with { Repo = "web" })], ifHash: null, Token);

        var page = Load().Index!.Pages!.Single(static p => p.Id?.Value == "schedule-week");
        Assert.Equal("Schedule week", page.Name);
        Assert.Equal("web", page.Repo);
        Assert.Equal("surface:route/GET:/schedule/week", page.Surface);
        var action = Assert.Single(page.Actions!);
        Assert.Equal(("submit", "Submit schedule", "action:SchedulePage.submit-schedule"), (action.Id?.Value, action.Name, action.Action));
        Assert.Equal(["billing", "concept:approval-workflow"], action.Concepts);
    }

    [Fact]
    public async Task A_page_for_a_repo_goes_into_that_repos_own_index_pages()
    {
        Put(Profile);

        var done = await Writer().Write([Set("schedule-week", Week, repo: "web"), Set("health", new PageSettings(Name: "Health"), repo: "api")], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        var profile = Load();
        Assert.Equal(["settings"], PageIds(profile.Index!.Pages), StringComparer.Ordinal);
        Assert.Equal(["schedule-week"], PageIds(profile.Repos!.Single(static r => r.Name == "web").Index!.Pages), StringComparer.Ordinal);
        Assert.Equal(50, profile.Repos!.Single(static r => r.Name == "web").Index!.MaxFileKB);
        Assert.Equal(["health"], PageIds(profile.Repos!.Single(static r => r.Name == "api").Index!.Pages), StringComparer.Ordinal);
        Assert.Contains("    path: /w   # the app", Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_page_for_a_repo_the_profile_does_not_name_is_refused_and_nothing_is_written()
    {
        Put(Profile);

        var done = await Writer().Write([Set("schedule-week", Week, repo: "admin")], ifHash: null, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("repos.admin.index.pages", done.Error.Message, StringComparison.Ordinal);
        Assert.Equal(Profile, Text());
    }

    [Fact]
    public async Task Removing_a_page_takes_only_its_own_lines()
    {
        Put(Profile);
        _ = await Writer().Write([Set("schedule-week", Week, repo: "web")], ifHash: null, Token);

        var done = await Writer().Write([new PageEdit(PagePinId.From("schedule-week"), Entry: null, Repo: "web"), new PageEdit(PagePinId.From("settings"), Entry: null)], ifHash: null, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        var profile = Load();
        Assert.Empty(profile.Index!.Pages!);
        Assert.Empty(profile.Repos!.Single(static r => r.Name == "web").Index!.Pages!);
        Assert.Contains("    # the money one", Text(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_concept_and_a_page_in_one_write_land_together_under_one_hash()
    {
        Put(Profile);
        Assert.Null(YamlToJson.TryConvert(Text(), out var json));
        var hash = ProfileReading.FromJson(json, ProfilePath, ProfileFiles.Options).Value.Hash;

        var done = await Writer().Write([new ConceptEdit(ConceptPinId.From("approval-workflow"), new ConceptSettings(Name: "Approval workflow")), Set("schedule-week", Week)], hash, Token);

        Assert.True(done.IsSuccess, done.IsSuccess ? null : done.Error.Message);
        var profile = Load();
        Assert.Equal(["billing", "approval-workflow"], profile.Index!.Concepts!.Select(static c => c.Id?.Value), StringComparer.Ordinal);
        Assert.Equal(["settings", "schedule-week"], PageIds(profile.Index.Pages), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_stale_hash_writes_no_page()
    {
        Put(Profile);
        Assert.Null(YamlToJson.TryConvert(Text(), out var json));
        var hash = ProfileReading.FromJson(json, ProfilePath, ProfileFiles.Options).Value.Hash;
        await File.AppendAllTextAsync(ProfilePath, "# edited by hand\n", Token);

        var done = await Writer().Write([Set("schedule-week", Week)], hash, Token);

        Assert.Equal("SND-PROFILE-003", done.Error.Code);
        Assert.DoesNotContain("schedule-week", Text(), StringComparison.Ordinal);
    }
}
