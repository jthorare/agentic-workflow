using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.ProfileLive;

/// <summary>Profile edits apply on save (#2258 PR 1): a valid write is what the next read returns, an invalid one writes nothing, and --if-hash stays as optimistic concurrency.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ProfileLiveTests : IDisposable
{
    private const string Profile = "# mine\nschemaVersion: 1\nuser: me\nrepos: []\ncontext:\n  models:\n    labeling:\n      pick: off   # default\n";

    private readonly TempHome _home = new();
    private readonly ProfileFiles _files;
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public ProfileLiveTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
        _files = new ProfileFiles(_home.Path);
        _files.Write(Profile);
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "auth"));
        File.WriteAllText(Path.Combine(_home.Path, "auth", "human.token"), "a-human-token\n");   // the per-start CLI token 'profile set' still checks
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _files.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ProfilePath => _files.Settings.ProfilePath;

    private LiveApprovedProfile Live() => new(_files.Source);

    private Task<int> Run(params string[] args) =>
        CommandTable.Run(args, name => name == "AW_SINDRI_HOME" ? _home.Path : null, TimeProvider.System);

    private async Task<ProfileHash> FileHash() => (await _files.Source.Load(Token)).Value.Hash;

    [Fact, Trait("Lane", "integration")]
    public async Task A_valid_profile_set_is_live_for_the_next_read()
    {
        var before = await FileHash();

        var code = await Run("profile", "set", "context.models.labeling.pick", "jev");

        Assert.Equal(0, code);
        var latest = await Live().Latest(Token);   // no approve step in between
        Assert.True(latest.IsSuccess);
        Assert.NotNull(latest.Value);
        Assert.NotEqual(before, latest.Value.Hash);
        Assert.Equal(await FileHash(), latest.Value.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_invalid_profile_edit_is_refused_and_writes_nothing()
    {
        var before = await FileHash();

        var code = await Run("profile", "set", "context.models.labeling.pick", "maybe");

        Assert.Equal(1, code);
        Assert.Contains("SND-CONTEXT-001", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(Profile, await File.ReadAllTextAsync(ProfilePath, Token));
        var latest = await Live().Latest(Token);   // still the profile that was there
        Assert.True(latest.IsSuccess);
        Assert.NotNull(latest.Value);
        Assert.Equal(before, latest.Value.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task IfHash_refuses_a_write_when_the_file_moved()
    {
        var writer = new YamlProfileIndexWriter(_files.Settings, ProfileFiles.Options);
        IndexEdit[] edit = [new ConceptEdit(ConceptPinId.From("billing"), new ConceptSettings(Name: "Billing"))];
        var stale = await FileHash();
        Assert.True((await writer.Write(edit, ifHash: null, Token)).IsSuccess);   // the file moves under the caller's hash
        var afterMove = await File.ReadAllTextAsync(ProfilePath, Token);

        var refused = await writer.Write(edit, stale, Token);

        Assert.Equal("SND-PROFILE-003", refused.Error.Code);
        Assert.Equal(afterMove, await File.ReadAllTextAsync(ProfilePath, Token));
        var latest = await Live().Latest(Token);
        Assert.NotNull(latest.Value);
        var current = latest.Value.Hash;   // the hash Latest hands out is the one --if-hash wants
        Assert.NotEqual(stale, current);
        Assert.True((await writer.Write(edit, current, Token)).IsSuccess);
    }
}
