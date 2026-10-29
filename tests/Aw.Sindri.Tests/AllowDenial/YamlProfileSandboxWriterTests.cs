using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.AllowDenial;

/// <summary>The profile edit Allow makes on the live file (sandbox spec §3.6): the entry lands under 'sandbox.repos.&lt;repo&gt;', every other key and comment stays, and an entry already there is not written twice.</summary>
[Lane(TestLane.Integration)]
public sealed class YamlProfileSandboxWriterTests : IDisposable
{
    private const string Home = "/home/o";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-sandbox-writer-" + RealTime.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ProfilePath => Path.Combine(_dir, "profile.yaml");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private YamlProfileSandboxWriter Writer() => new(new SindriSettings(_dir, ProfilePath)) { Home = Home };

    private void Put(string text)
    {
        _ = Directory.CreateDirectory(_dir);
        File.WriteAllText(ProfilePath, text);
    }

    private string Text() => File.ReadAllText(ProfilePath);

    private LoadedProfile Load()
    {
        Assert.Null(YamlToJson.TryConvert(Text(), out var json));
        var loaded = ProfileReading.FromJson(json, ProfilePath, ProfileFiles.Options);
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
        return loaded.Value;
    }

    [Fact]
    public async Task A_profile_with_no_sandbox_section_gets_one_for_the_repo()
    {
        Put("schemaVersion: 1\nuser: thor\n# keep me\nrepos:\n  - name: web\n    path: /work/web\n");

        var written = await Writer().Allow("web", DenialKind.Network, "github.com", Token);

        Assert.True(written.IsSuccess, written.IsError ? written.Error.ToString() : null);
        Expect.Equal(("github.com", true), (written.Value.Entry, written.Value.Added));
        Assert.Equal("schemaVersion: 1\nuser: thor\n# keep me\nrepos:\n  - name: web\n    path: /work/web\nsandbox:\n  repos:\n    web:\n      hosts:\n        - \"github.com\"\n", Text());
        _ = Load();
    }

    [Fact]
    public async Task A_host_is_appended_to_the_repos_existing_list_and_nothing_else_moves()
    {
        const string Before = "schemaVersion: 1\nuser: thor\nsandbox:\n  limits:\n    sessions: 2   # three is too many\n  repos:\n    web:\n      hosts:\n        - registry.npmjs.org\n      cachePaths: [.cache/npm]\n    api:\n      hosts: [nuget.org]\nservices:\n  heartbeat: { every: 5s }\n";
        Put(Before);

        var written = await Writer().Allow("web", DenialKind.Network, "github.com", Token);

        Assert.True(written.IsSuccess, written.IsError ? written.Error.ToString() : null);
        Assert.Equal(Before.Replace("        - registry.npmjs.org\n", "        - registry.npmjs.org\n        - \"github.com\"\n", StringComparison.Ordinal), Text());
    }

    [Fact]
    public async Task A_home_path_is_written_relative_and_read_only()
    {
        Put("schemaVersion: 1\nuser: thor\nsandbox:\n  repos:\n    web:\n      hosts: []\n");

        var written = await Writer().Allow("web", DenialKind.Path, "/home/o/.cache/tool", Token);

        Assert.True(written.IsSuccess, written.IsError ? written.Error.ToString() : null);
        Assert.Equal(".cache/tool", written.Value.Entry);
        Assert.Equal("schemaVersion: 1\nuser: thor\nsandbox:\n  repos:\n    web:\n      hosts: []\n      homeAllow:\n        - path: \".cache/tool\"\n          mode: ro\n", Text());
        _ = Load();
    }

    [Fact]
    public async Task An_entry_already_in_the_file_is_not_written_again()
    {
        const string Before = "schemaVersion: 1\nuser: thor\nsandbox:\n  repos:\n    web:\n      hosts:\n        - github.com\n";
        Put(Before);

        var written = await Writer().Allow("web", DenialKind.Network, "github.com", Token);

        Assert.True(written.IsSuccess, written.IsError ? written.Error.ToString() : null);
        Assert.False(written.Value.Added);
        Assert.Equal(Before, Text());
    }

    [Fact]
    public async Task A_flow_style_hosts_list_with_entries_is_a_layout_the_edit_leaves_alone()
    {
        const string Before = "schemaVersion: 1\nuser: thor\nsandbox:\n  repos:\n    web:\n      hosts: [registry.npmjs.org]\n";
        Put(Before);

        var written = await Writer().Allow("web", DenialKind.Network, "github.com", Token);

        Assert.True(written.IsError);
        Assert.Equal(SindriErrors.ProfileInvalid.Code, written.Error.Code);
        Assert.Equal(Before, Text());
    }

    [Theory]
    [InlineData(DenialKind.Path, "/var/lib/other")]
    [InlineData(DenialKind.Path, "/home/o")]
    [InlineData(DenialKind.Network, "https://github.com")]
    [InlineData(DenialKind.Policy, "git push")]
    public async Task A_target_the_profile_cannot_hold_is_refused_and_the_file_is_untouched(DenialKind kind, string target)
    {
        const string Before = "schemaVersion: 1\nuser: thor\n";
        Put(Before);

        var written = await Writer().Allow("web", kind, target, Token);

        Assert.True(written.IsError);
        Assert.Equal(SindriErrors.SandboxAllowRefused.Code, written.Error.Code);
        Assert.Equal(Before, Text());
    }

    [Fact]
    public async Task A_missing_profile_cannot_be_read()
    {
        var written = await Writer().Allow("web", DenialKind.Network, "github.com", Token);

        Assert.True(written.IsError);
        Assert.Equal(SindriErrors.ProfileMissing.Code, written.Error.Code);
    }
}
