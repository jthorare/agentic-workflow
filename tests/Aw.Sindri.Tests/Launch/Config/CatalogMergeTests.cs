using Aw.Sindri.Core.Launch;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>How launcher.json's entries merge over the built-ins, and what the merged entry records: source, forms, argv hash.</summary>
public sealed class CatalogMergeTests
{
    private const string Aider = "\"aider\": { \"label\": \"Aider\", \"bin\": \"aider\", \"open\": [\"{bin}\", \"{seed}\"], \"sessionId\": \"none\" }";

    [Fact, Trait("Lane", "unit")]
    public void Override_without_override_true_is_SND_LAUNCH_011()
    {
        var read = Infrastructure.Launch.LauncherConfigFile.Read(SessionFixtures.Builtins(), LauncherText.Bytes("""{ "schema": 1, "agents": { "codex": { "bin": "/x/codex" } } }"""));

        Assert.Equal("SND-LAUNCH-011", read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_override_replaces_only_the_fields_it_names()
    {
        var config = LauncherText.Strict("""{ "schema": 1, "agents": { "codex": { "override": true, "label": "Codex CLI", "transcripts": null } } }""");

        var codex = LauncherText.Entry(config, "codex");

        Assert.Equal(("Codex CLI", "codex", null), (codex.Label, codex.Bin, codex.Transcripts));
        Assert.Equal(["{bin}", "-C", "{cwd}", "{seed}"], codex.Open);
        Assert.Equal(["{bin}", "resume", "{sessionId}"], codex.Resume);
        Assert.Equal((SessionIdMode.Reported, ModelForm.CodexFlag, PolicyForm.CodexOverrides), (codex.SessionIdMode, codex.ModelForm, codex.PolicyForm));
    }

    [Fact, Trait("Lane", "unit")]
    public void Entries_record_where_they_came_from()
    {
        var config = LauncherText.Strict($$"""{ "schema": 1, "agents": { "codex": { "override": true, "label": "Codex CLI" }, {{Aider}} } }""");

        Assert.Equal(
            [("claude-code", CatalogSource.Builtin), ("codex", CatalogSource.Override), ("cursor", CatalogSource.Builtin), ("aider", CatalogSource.Added)],
            config.Agents.Select(static a => (a.Id.Value, a.Source)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Source_is_recorded_the_same_when_the_file_is_read_section_by_section()
    {
        var config = LauncherText.Isolated($$"""{ "schema": 1, "agents": { "cursor": { "override": true, "label": "C" }, "broken": { "label": "x" }, {{Aider}} } }""");

        Assert.Equal(
            [("claude-code", CatalogSource.Builtin), ("codex", CatalogSource.Builtin), ("cursor", CatalogSource.Override), ("aider", CatalogSource.Added)],
            config.Agents.Select(static a => (a.Id.Value, a.Source)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_builtins_carry_their_forms_and_every_entry_an_argv_hash()
    {
        var config = LauncherText.Strict("""{ "schema": 1 }""");

        Assert.Equal(
            [("claude-code", ModelForm.ClaudeFlag, PolicyForm.ClaudeSettingsFile), ("codex", ModelForm.CodexFlag, PolicyForm.CodexOverrides), ("cursor", ModelForm.None, PolicyForm.None)],
            config.Agents.Select(static a => (a.Id.Value, a.ModelForm, a.PolicyForm)));
        Assert.All(config.Agents, static a => Assert.Matches("^sha256:[0-9a-f]{64}$", a.ArgvHash));
    }

    [Fact, Trait("Lane", "unit")]
    public void Override_keeps_builtin_forms_unless_named()
    {
        var kept = LauncherText.Entry(LauncherText.Strict("""{ "schema": 1, "agents": { "claude-code": { "override": true, "label": "C" } } }"""), "claude-code");
        var named = LauncherText.Entry(LauncherText.Strict("""{ "schema": 1, "agents": { "claude-code": { "override": true, "model": "none", "policy": "none" } } }"""), "claude-code");

        Assert.Equal((ModelForm.ClaudeFlag, PolicyForm.ClaudeSettingsFile), (kept.ModelForm, kept.PolicyForm));
        Assert.Equal((ModelForm.None, PolicyForm.None), (named.ModelForm, named.PolicyForm));
    }

    [Fact, Trait("Lane", "unit")]
    public void Added_entry_defaults_both_forms_to_none()
    {
        var aider = LauncherText.Entry(LauncherText.Strict($$"""{ "schema": 1, "agents": { {{Aider}} } }"""), "aider");

        Assert.Equal((ModelForm.None, PolicyForm.None), (aider.ModelForm, aider.PolicyForm));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_form_name_of_the_schema_maps_to_its_enum_value()
    {
        var config = LauncherText.Strict(File.ReadAllText(Path.Combine(SessionFixtures.Launcher, "model-policy-every-form.json")));

        List<(ModelForm, PolicyForm)> expected = [(ModelForm.ClaudeFlag, PolicyForm.ClaudeSettingsFile), (ModelForm.CodexFlag, PolicyForm.CodexOverrides), (ModelForm.CursorFlag, PolicyForm.CursorConfig), (ModelForm.None, PolicyForm.None)];
        string[] ids = ["a1", "a2", "a3", "a4"];

        Assert.Equal(expected, ids.Select(id => LauncherText.Entry(config, id)).Select(static a => (a.ModelForm, a.PolicyForm)));
    }
}
