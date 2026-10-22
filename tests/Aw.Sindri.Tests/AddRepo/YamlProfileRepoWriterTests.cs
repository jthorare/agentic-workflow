using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The profile edit AddRepo makes on the live file (setup-once spec §5, §5.1): every other key and comment stays, the file is created with the defaults, and a write that loses a race reads again once.</summary>
[Lane(TestLane.Integration)]
public sealed class YamlProfileRepoWriterTests : IDisposable
{
    private static readonly string[] _effects = ["effect.claim.ref", "effect.notify.desk"];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-repo-writer-" + RealTime.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ProfilePath => Path.Combine(_dir, "profile.yaml");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private YamlProfileRepoWriter Writer() => new(new SindriSettings(_dir, ProfilePath));

    private string Put(string text)
    {
        _ = Directory.CreateDirectory(_dir);
        File.WriteAllText(ProfilePath, text);
        return ProfilePath;
    }

    private LoadedProfile Load()
    {
        Assert.Null(YamlToJson.TryConvert(File.ReadAllText(ProfilePath), out var json));
        var loaded = ProfileReading.FromJson(json, ProfilePath, ProfileFiles.Options);
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
        return loaded.Value;
    }

    [Fact]
    public async Task The_first_repo_creates_the_profile_with_the_defaults_and_the_repo()
    {
        var done = await Writer().Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal(Core.Setup.RepoAppended.Created, done.Value);
        var profile = Load().Profile;
        Assert.Equal(["effect.claim.ref", "effect.notify.desk"], profile.Workflow!.Effects);
        Assert.Equal([("demo", "/work/demo")], profile.Repos!.Select(static r => (r.Name, r.Path)));
        Assert.Equal(1, profile.SchemaVersion);
    }

    [Fact]
    public async Task A_repo_joins_an_existing_block_and_every_other_line_stays_as_it_was()
    {
        _ = Put("# my profile\nschemaVersion: 1\nuser: thor   # me\nrepos:\n  - name: first\n    path: /work/first   # the first one\n\nservices:\n  heartbeat: { every: 30s }\n");

        var done = await Writer().Append(new RepoEntry("second", "/work/second"), _effects, Token);

        Assert.Equal(Core.Setup.RepoAppended.Appended, done.Value);
        var text = await File.ReadAllTextAsync(ProfilePath, Token);
        Assert.Contains("# my profile", text, StringComparison.Ordinal);
        Assert.Contains("user: thor   # me", text, StringComparison.Ordinal);
        Assert.Contains("path: /work/first   # the first one", text, StringComparison.Ordinal);
        Assert.Contains("services:\n  heartbeat: { every: 30s }", text, StringComparison.Ordinal);
        Assert.Equal(["first", "second"], Load().Profile.Repos!.Select(static r => r.Name), StringComparer.Ordinal);
        Assert.True(text.IndexOf("name: \"second\"", StringComparison.Ordinal) < text.IndexOf("services:", StringComparison.Ordinal), "the new entry sits in the repos block");
    }

    [Fact]
    public async Task An_empty_flow_list_becomes_a_block()
    {
        _ = Put("schemaVersion: 1\nuser: thor\nrepos: []\n");

        _ = await Writer().Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal(["demo"], Load().Profile.Repos!.Select(static r => r.Name), StringComparer.Ordinal);
        Assert.DoesNotContain("[]", await File.ReadAllTextAsync(ProfilePath, Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_profile_with_no_repos_key_gets_one_at_the_end()
    {
        _ = Put("schemaVersion: 1\nuser: thor\nservices:\n  heartbeat: { every: 30s }\n");

        var done = await Writer().Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal(Core.Setup.RepoAppended.Appended, done.Value);
        Assert.Equal(["demo"], Load().Profile.Repos!.Select(static r => r.Name), StringComparer.Ordinal);
        Assert.True(Load().Profile.Services!.ContainsKey("heartbeat"));
    }

    [Fact]
    public async Task A_flow_list_with_entries_is_left_alone_and_the_lines_to_add_are_in_the_error()
    {
        var before = "schemaVersion: 1\nuser: thor\nrepos: [ { name: a, path: /work/a } ]\n";
        _ = Put(before);

        var done = await Writer().Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("/work/demo", done.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllTextAsync(ProfilePath, Token));
    }

    [Fact]
    public async Task A_profile_that_does_not_parse_is_SND_PROFILE_002_and_is_not_touched()
    {
        var before = "schemaVersion: 1\nuser: [oops\n";
        _ = Put(before);

        var done = await Writer().Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Equal(before, await File.ReadAllTextAsync(ProfilePath, Token));
    }

    [Fact]
    public async Task A_name_that_differs_only_in_case_is_taken()
    {
        _ = await Writer().Append(new RepoEntry("Demo", "/work/demo"), _effects, Token);

        var done = await Writer().Append(new RepoEntry("demo", "/work/other"), _effects, Token);

        Assert.Equal("SND-SETUP-002", done.Error.Code);
    }

    [Fact]
    public async Task Writers_at_once_all_land_in_one_file()
    {
        _ = await Writer().Append(new RepoEntry("first", "/work/first"), _effects, Token);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Writer().Append(new RepoEntry(string.Create(CultureInfo.InvariantCulture, $"r{i}"), string.Create(CultureInfo.InvariantCulture, $"/work/r{i}")), _effects, Token).AsTask()));

        Assert.All(results, static r => Assert.True(r.IsSuccess, r.IsSuccess ? null : r.Error.Message));
        Assert.Equal(7, Load().Profile.Repos!.Count);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task A_file_that_changes_between_the_read_and_the_write_is_read_again_and_a_second_change_says_to_try_again()
    {
        _ = Put("schemaVersion: 1\nuser: thor\n");
        var writer = new YamlProfileRepoWriter(new SindriSettings(_dir, ProfilePath)) { BeforeWrite = static (path, attempt) => File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture, $"# changed {attempt}\n")) };

        var done = await writer.Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("changed while Sindri was writing it. Try again.", done.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("demo", await File.ReadAllTextAsync(ProfilePath, Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_that_changes_once_is_read_again_and_the_write_lands()
    {
        _ = Put("schemaVersion: 1\nuser: thor\n");
        var writer = new YamlProfileRepoWriter(new SindriSettings(_dir, ProfilePath)) { BeforeWrite = static (path, attempt) => { if (attempt == 0) { File.AppendAllText(path, "# changed\n"); } } };

        var done = await writer.Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal(Core.Setup.RepoAppended.Appended, done.Value);
        Assert.Contains("# changed", await File.ReadAllTextAsync(ProfilePath, Token), StringComparison.Ordinal);
        Assert.Equal(["demo"], Load().Profile.Repos!.Select(static r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_file_is_owner_only()
    {
        _ = await Writer().Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ProfilePath));
        }
    }

    [Fact]
    public async Task A_file_with_windows_line_endings_keeps_them()
    {
        _ = Put("schemaVersion: 1\r\nuser: thor\r\nrepos:\r\n  - name: first\r\n    path: /work/first\r\n");

        _ = await Writer().Append(new RepoEntry("second", "/work/second"), _effects, Token);

        var text = await File.ReadAllTextAsync(ProfilePath, Token);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.StartsWith("schemaVersion: 1\r\nuser: thor\r\nrepos:\r\n  - name: first\r\n    path: /work/first\r\n", text, StringComparison.Ordinal);
        Assert.Equal(["first", "second"], Load().Profile.Repos!.Select(static r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_write_that_the_system_refuses_says_so_with_the_reason_and_not_that_the_profile_is_missing()
    {
        _ = Directory.CreateDirectory(ProfilePath);   // a folder where the file should be: the move onto it fails

        var done = await Writer().Append(new RepoEntry("demo", "/work/demo"), _effects, Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
        Assert.Contains("can't be written:", done.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fix: make the file and its folder writable", done.Error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }
}
