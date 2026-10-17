using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.judge.providers' (DT-J plan Q8): read, checked, and kept out of the canonical JSON of a profile that doesn't set it.</summary>
public sealed class JudgeProviderSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    [Fact, Trait("Lane", "integration")]
    public async Task Workflow_judge_providers_reads_and_keeps_old_hashes()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  judge:\n    providers: [jev, claude-cli]\nrepos:\n  - name: app\n    path: /a\n    workflow:\n      judge:\n        providers: [codex-cli]\n");

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        Assert.Equal(["jev", "claude-cli"], loaded.Value.Profile.Workflow?.Judge?.Providers);
        Assert.Equal(["codex-cli"], loaded.Value.Profile.Repos?[0].Workflow?.Judge?.Providers);
        var plain = Encoding.UTF8.GetString(AwJson.Serialize(ProfileRules.Check(_valid).Value, SliceHarness.Json));
        Assert.DoesNotContain("judge", plain, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_provider_name_is_a_profile_error()
    {
        var error = ProfileRules.Check(_valid with { Workflow = new(Judge: new(Providers: ["claude-cli", "gpt"])) }).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith("workflow.judge.providers[1]: 'gpt' is not a judge provider", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_list_wins_over_the_box_list()
    {
        var profile = _valid with
        {
            Workflow = new(Judge: new(Providers: ["jev"])),
            Repos = [new RepoEntry("app", "/a", Workflow: new(Judge: new(Providers: ["codex-cli"]))), new RepoEntry("lib", "/b")],
        };

        Assert.Equal(["codex-cli", "rules"], JudgeProviders.Effective(profile, "app", recipeLiteral: null));
        Assert.Equal(["jev", "rules"], JudgeProviders.Effective(profile, "lib", recipeLiteral: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void Absent_key_leaves_jev_out() =>
        Assert.Equal(["claude-cli", "codex-cli", "cursor-cli", "rules"], JudgeProviders.Effective(_valid, repo: null, recipeLiteral: null));

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_literal_narrows_but_never_adds_jev()
    {
        Assert.Equal(["claude-cli", "rules"], JudgeProviders.Effective(_valid, repo: null, recipeLiteral: ["jev", "claude-cli"]));
        var optIn = _valid with { Workflow = new(Judge: new(Providers: ["jev", "claude-cli"])) };
        Assert.Equal(["jev", "rules"], JudgeProviders.Effective(optIn, repo: null, recipeLiteral: ["jev", "codex-cli"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Rules_is_always_kept()
    {
        Assert.Equal(["claude-cli", "rules"], JudgeProviders.Effective(_valid with { Workflow = new(Judge: new(Providers: ["claude-cli"])) }, repo: null, recipeLiteral: null));
        Assert.Equal(["rules"], JudgeProviders.Effective(_valid, repo: null, recipeLiteral: ["codex-cli-not-listed"]));
        Assert.Equal(["claude-cli", "rules"], JudgeProviders.Effective(_valid, repo: null, recipeLiteral: ["rules", "claude-cli"]));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_profile_with_a_workflow_section_and_a_repo_but_no_judge_key_keeps_the_hash_it_had()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  deskTimeout: 1m\nrepos:\n  - name: app\n    path: /a\n");

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        Assert.Equal("f0c0d2e9113ea87a51435a8c47fe1d605e1976c9c40111572426613f5c2d5545", loaded.Value.Hash.Value);   // pinned: a new optional key left out when null must not move the hash of a profile that never set it
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_provider_in_a_repo_list_names_the_repo_and_the_index()
    {
        var profile = _valid with { Repos = [new RepoEntry("app", "/a", Workflow: new(Judge: new(Providers: ["rules", "gpt"])))] };

        var error = ProfileRules.Check(profile).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith("repos.app.workflow.judge.providers[1]: 'gpt' is not a judge provider", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_the_box_and_the_repo_provider_keys()
    {
        var live = ProfileRules.Check(_valid).Value;
        var edited = live with
        {
            Workflow = new(Judge: new(Providers: ["jev"])),
            Repos = [new RepoEntry("app", "/a", Workflow: new(Judge: new(Providers: ["rules"])))],
        };

        Assert.Equal(["repos.app", "repos.app.workflow.judge.providers", "workflow.judge.providers"], [.. ProfileDiff.Between(live, edited).Order(StringComparer.Ordinal)]);
    }
}
