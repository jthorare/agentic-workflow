using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'setup:' block (setup-once spec §7.1): 'mcp: auto | off' (default auto), left out of the canonical JSON when absent so an approved profile keeps its hash.</summary>
public sealed class SetupSettingsTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\nrepos:\n  - { name: web, path: /work/web }\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_the_setup_mcp_choice()
    {
        _files.Write(Base + "setup:\n  mcp: off\n");

        var profile = (await _files.Source.Load(Token)).Value.Profile;

        Assert.Equal("off", profile.Setup!.Mcp);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_profile_without_a_setup_block_keeps_its_hash_and_one_with_it_does_not()
    {
        _files.Write(Base);
        var plain = (await _files.Source.Load(Token)).Value.Hash;
        _files.Write(Base + "setup:\n  mcp: off\n");
        var withSetup = (await _files.Source.Load(Token)).Value.Hash;

        Assert.NotEqual(plain, withSetup);
        _files.Write(Base);
        Assert.Equal(plain, (await _files.Source.Load(Token)).Value.Hash);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, true)]
    [InlineData("auto", true)]
    [InlineData("off", false)]
    public void Registration_is_automatic_unless_the_profile_says_off(string? mcp, bool automatic)
    {
        var profile = new SindriProfile(1, "thor", Setup: mcp is null ? null : new SetupSettings(mcp));

        Assert.Equal(automatic, SetupSettings.McpAutomatic(profile));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_block_at_all_is_automatic() => Assert.True(SetupSettings.McpAutomatic(new SindriProfile(1, "thor")));

    [Fact, Trait("Lane", "unit")]
    public void Any_other_choice_is_refused_before_approval_naming_its_key()
    {
        var error = ProfileRules.Check(new SindriProfile(1, "thor", Setup: new SetupSettings("sometimes"))).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains("setup.mcp", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("auto")]
    [InlineData("off")]
    [InlineData(null)]
    public void The_two_choices_pass(string? mcp) => Assert.True(ProfileRules.Check(new SindriProfile(1, "thor", Setup: new SetupSettings(mcp))).IsSuccess);

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_the_first_run_keys()
    {
        _files.Write(Base + "setup:\n  skipAccounts: true\n  skipFirstWorkflow: true\n  oauth:\n    github: Iv1.abc123\n    linear: lin_abc\n");

        var setup = (await _files.Source.Load(Token)).Value.Profile.Setup!;

        Assert.Equal((true, true, "Iv1.abc123", "lin_abc"), (setup.SkipAccounts, setup.SkipFirstWorkflow, setup.Oauth!.Github, setup.Oauth.Linear));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_client_id_with_a_space_is_refused_naming_its_key()
    {
        var github = ProfileRules.Check(new SindriProfile(1, "thor", Setup: new SetupSettings { Oauth = new SetupOauthSettings(Github: "two words") })).Error;
        var linear = ProfileRules.Check(new SindriProfile(1, "thor", Setup: new SetupSettings { Oauth = new SetupOauthSettings(Linear: " ") })).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, github.Code);
        Assert.Contains("setup.oauth.github", github.Message, StringComparison.Ordinal);
        Assert.Contains("setup.oauth.linear", linear.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_skip_flags_and_a_client_id_pass()
    {
        var setup = new SetupSettings { SkipAccounts = true, SkipFirstWorkflow = true, Oauth = new SetupOauthSettings("Iv1.abc123", "lin_abc") };

        Assert.True(ProfileRules.Check(new SindriProfile(1, "thor", Setup: setup)).IsSuccess);
    }
}
