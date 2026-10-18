using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Profile;

public sealed class ProfileSourceTests : IDisposable
{
    private const string Valid = "# my profile\nschemaVersion: 1\nuser: thor\nservices:\n  heartbeat: { every: 30s }\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task A_valid_profile_loads_typed_with_defaults()
    {
        _files.Write(Valid);

        var loaded = (await _files.Source.Load(Token)).Value;

        Assert.Equal("thor", loaded.Profile.User);
        Assert.Equal("30s", loaded.Profile.Services!["heartbeat"].Every);
        Assert.Equal(7410, loaded.Profile.Api!.Port);
        Assert.Equal(_files.Settings.ProfilePath, loaded.Source);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Comments_and_key_order_keep_the_hash_and_a_value_changes_it()
    {
        _files.Write(Valid);
        var first = (await _files.Source.Load(Token)).Value.Hash;
        _files.Write("services:\n  heartbeat:\n    every: 30s   # every half minute\nuser: thor\nschemaVersion: 1\n");
        var reordered = (await _files.Source.Load(Token)).Value.Hash;
        _files.Write(Valid.Replace("30s", "31s", StringComparison.Ordinal));
        var changed = (await _files.Source.Load(Token)).Value.Hash;

        Assert.Equal(first, reordered);
        Assert.NotEqual(first, changed);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Key_order_inside_context_keeps_the_hash()
    {
        _files.Write(Valid);
        var without = (await _files.Source.Load(Token)).Value.Hash;
        _files.Write(Valid + "context:\n  deadline: 4s\n  docs: { globs: [\"docs/**/*.md\"] }\n");
        var first = (await _files.Source.Load(Token)).Value.Hash;
        _files.Write(Valid + "context:\n  docs: { globs: [\"docs/**/*.md\"] }\n  deadline: 4s\n");
        var reordered = (await _files.Source.Load(Token)).Value.Hash;

        Assert.Equal(first, reordered);
        Assert.NotEqual(without, first);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("schemaVersion: 1\nuser: thor\ncolour: blue\n", "'colour' is not a profile key here (allowed: schemaVersion, user, tenant, repos, services, api, workflow, context, index, launch, agents, sandbox)")]
    [InlineData("schemaVersion: one\nuser: thor\n", "schemaVersion: expected a whole number")]
    [InlineData("schemaVersion: 1\nuser: thor\nrepos: { a: 1 }\n", "repos: expected a list")]
    [InlineData("schemaVersion: 1\n", "user")]
    [InlineData("schemaVersion: 1\nuser: thor\nservices:\n  heartbeat: { every: 0s }\n", "services.heartbeat.every")]
    [InlineData("schemaVersion: 2\nuser: thor\n", "schemaVersion: is 2")]
    [InlineData("schemaVersion: 1\nuser: thor\nservices:\n  heartbeat: { every: 30s, jitter: 1 }\n", "'jitter' is not a profile key here (allowed: every)")]
    public async Task An_invalid_profile_names_the_file_the_key_and_the_fix(string yaml, string key)
    {
        _files.Write(yaml);

        var error = (await _files.Source.Load(Token)).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains(_files.Settings.ProfilePath, error.Message, StringComparison.Ordinal);
        Assert.Contains(key, error.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("BytePositionInLine", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_profile_says_how_to_create_one()
    {
        var error = (await _files.Source.Load(Token)).Error;

        Assert.Equal(SindriErrors.ProfileMissing.Code, error.Code);
        Assert.Contains("schemaVersion: 1", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task An_approval_is_the_latest_snapshot_owner_only_and_survives_a_live_edit()
    {
        Assert.Null((await _files.Approvals.Latest(Token)).Value);
        _files.Write(Valid);
        var approved = (await _files.Source.Load(Token)).Value;
        _ = await _files.Approvals.Save(approved, Token);
        _files.Write(Valid.Replace("30s", "7s", StringComparison.Ordinal));

        var latest = (await _files.Approvals.Latest(Token)).Value;

        Assert.Equal(approved.Hash, latest!.Hash);
        Assert.Equal("30s", latest.Profile.Services!["heartbeat"].Every);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(latest.Source));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_hand_edited_snapshot_is_not_trusted()
    {
        _files.Write(Valid);
        var approved = (await _files.Source.Load(Token)).Value;
        _ = await _files.Approvals.Save(approved, Token);
        var snapshot = Path.Combine(_files.Settings.ApprovalsDirectory, approved.Hash.Value, "profile.json");
        await File.WriteAllTextAsync(snapshot, (await File.ReadAllTextAsync(snapshot, Token)).Replace("30s", "5s", StringComparison.Ordinal), Token);

        Assert.Equal(SindriErrors.ProfileNotApproved.Code, (await _files.Approvals.Latest(Token)).Error.Code);
    }
}
