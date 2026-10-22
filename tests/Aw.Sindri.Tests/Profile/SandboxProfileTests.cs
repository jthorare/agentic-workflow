using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'sandbox:' section (sandbox spec §9, SB18; SB-0).</summary>
public sealed class SandboxProfileTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\nrepos:\n  - { name: web, path: /work/web }\n";
    private static readonly string[] _everyKey =
    [
        "sandbox:",
        "  limits: { memory: 8GiB, cpu: 25%, tasks: 1024, sessions: 2, total: 50%, wallGrace: 5m, homeTmpfs: 128MiB, parkMax: 12h }",
        "  fs:",
        "    homeAllow: [ { path: .cargo, mode: ro }, { path: .nuget/packages, mode: rw } ]",
        "    denyExtra: [ /srv/secrets ]",
        "  network:",
        "    mode: learn",
        "    groups: { model: [ api.anthropic.com ], packages: [ \"*.nuget.org\", registry.npmjs.org ] }",
        "  repos:",
        "    web:",
        "      homeAllow: [ { path: .gem } ]",
        "      groups: [ packages ]",
        "      hosts: [ example.org ]",
        "      cachePaths: [ .cache/pip ]",
        "      login: { source: separate-login }",
        "      prepare: { dirs: [ { dir: sindri, tool: npm ci, allowScripts: [ better-sqlite3 ] } ] }",
        "  inner: on",
        "  credentials: { github: agent-app }",
        "  login: { source: separate-login, targets: { codex: owner-copy } }",
        "  attended: { wall: 8h }",
        "",
    ];

    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task Missing_sandbox_section_uses_defaults_and_keeps_the_hash()
    {
        _files.Write(Base);
        var plain = (await _files.Source.Load(Token)).Value;

        Assert.Null(plain.Profile.Sandbox);
        Assert.DoesNotContain("sandbox", System.Text.Json.JsonSerializer.Serialize(plain.Profile, ProfileFiles.Options), StringComparison.Ordinal);
        _files.Write(Base + "sandbox:\n  inner: off\n");
        Assert.NotEqual(plain.Hash, (await _files.Source.Load(Token)).Value.Hash);
        _files.Write(Base);
        Assert.Equal(plain.Hash, (await _files.Source.Load(Token)).Value.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_a_sandbox_section_has_the_canonical_json_it_had_before_SB0() =>
        Golden.Match(System.Text.Json.JsonSerializer.Serialize(new SindriProfile(1, "thor"), ProfileFiles.Options) + "\n", typeof(SandboxProfileTests).Assembly, "Golden/Sandbox/profile-without-sandbox.json", Normalization.None);

    [Fact, Trait("Lane", "integration")]
    public async Task An_empty_sandbox_section_is_valid_and_changes_the_hash()
    {
        _files.Write(Base);
        var plain = (await _files.Source.Load(Token)).Value;
        _files.Write(Base + "sandbox: {}\n");

        var empty = await _files.Source.Load(Token);

        Assert.True(empty.IsSuccess);
        Assert.NotEqual(plain.Hash, empty.Value.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_defaults_are_the_specs()
    {
        Assert.Equal(("auto", "50%", 2048, 3, "60%", "10m", "256MiB", "24h", "off"),
            (SandboxDefaults.Memory, SandboxDefaults.Cpu, SandboxDefaults.Tasks, SandboxDefaults.Sessions, SandboxDefaults.Total,
             SandboxDefaults.WallGrace, SandboxDefaults.HomeTmpfs, SandboxDefaults.ParkMax, SandboxDefaults.AttendedWall));
        Assert.Equal((ModelLoginSource.OwnerCopy, NetworkMode.Enforce, InnerSandbox.Auto, GithubCredentials.None),
            (SandboxDefaults.Login, SandboxDefaults.Network, SandboxDefaults.Inner, SandboxDefaults.Github));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Every_documented_key_reads()
    {
        _files.Write(Base + string.Join('\n', _everyKey) + "\n");

        var sandbox = (await _files.Source.Load(Token)).Value.Profile.Sandbox!;

        Assert.Equal((2, NetworkMode.Learn, InnerSandbox.On, GithubCredentials.AgentApp, ModelLoginSource.SeparateLogin, "8h"),
            (sandbox.Limits!.Sessions, sandbox.Network!.Mode, sandbox.Inner, sandbox.Credentials!.Github, sandbox.Login!.Source, sandbox.Attended!.Wall));
        Assert.Equal(ModelLoginSource.OwnerCopy, sandbox.Login.Targets!["codex"]);
        Assert.Equal(ModelLoginSource.SeparateLogin, sandbox.Repos!["web"].Login!.Source);
        Assert.Equal("npm ci", sandbox.Repos["web"].Prepare!.Dirs![0].Tool);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("limits: { sessions: 0 }", "sandbox.limits.sessions")]
    [InlineData("limits: { tasks: 0 }", "sandbox.limits.tasks")]
    [InlineData("limits: { tasks: 5000000 }", "sandbox.limits.tasks")]
    [InlineData("limits: { sessions: 1000 }", "sandbox.limits.sessions")]
    [InlineData("network: { groups: { model: [ a..b.test ] } }", "sandbox.network.groups.model")]
    [InlineData("limits: { memory: lots }", "sandbox.limits.memory")]
    [InlineData("limits: { cpu: 150% }", "sandbox.limits.cpu")]
    [InlineData("limits: { total: 0% }", "sandbox.limits.total")]
    [InlineData("limits: { wallGrace: soon }", "sandbox.limits.wallGrace")]
    [InlineData("limits: { parkMax: 1y }", "sandbox.limits.parkMax")]
    [InlineData("limits: { homeTmpfs: big }", "sandbox.limits.homeTmpfs")]
    [InlineData("attended: { wall: never }", "sandbox.attended.wall")]
    [InlineData("network: { groups: { model: [ \"https://x.test\" ] } }", "sandbox.network.groups.model")]
    [InlineData("network: { groups: { \"\": [ a.test ] } }", "sandbox.network.groups")]
    [InlineData("fs: { homeAllow: [ { path: /etc } ] }", "sandbox.fs.homeAllow")]
    [InlineData("fs: { denyExtra: [ relative/path ] }", "sandbox.fs.denyExtra")]
    [InlineData("repos: { web: { groups: [ \"\" ] } }", "sandbox.repos.web.groups")]
    public async Task A_bad_value_names_its_key_path(string block, string path)
    {
        _files.Write(Base + "sandbox:\n  " + block + "\n");

        var result = await _files.Source.Load(Token);

        Assert.False(result.IsSuccess);
        Assert.Contains(path, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("login: { source: shared-login }")]
    [InlineData("repos: { web: { login: { source: nope } } }")]
    [InlineData("login: { targets: { codex: nope } }")]
    [InlineData("agentHome: owner")]
    [InlineData("credentials: { github: token }")]
    public async Task An_unknown_source_or_key_is_a_profile_error(string block)
    {
        _files.Write(Base + "sandbox:\n  " + block + "\n");

        Assert.False((await _files.Source.Load(Token)).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_approve_preview_lists_sandbox_paths()
    {
        var before = new SindriProfile(1, "thor");
        var after = before with { Sandbox = new SandboxSettings(Limits: new SandboxLimits(Sessions: 2), Login: new SandboxLogin(Source: ModelLoginSource.SeparateLogin)) };

        var paths = ProfileDiff.Between(before, after);

        Assert.Equal(["sandbox.limits.sessions", "sandbox.login.source"], paths);
    }
}
