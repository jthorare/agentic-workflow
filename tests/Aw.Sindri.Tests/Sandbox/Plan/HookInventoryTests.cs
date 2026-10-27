using System.Text.RegularExpressions;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The hook inventory against the hooks that are actually installed (sandbox spec §3.1): a hook that can't run fails open, so a drift here switches a guard off silently.</summary>
public sealed partial class HookInventoryTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
    }

    private static IReadOnlyList<string> HookFiles() =>
        [.. Directory.GetFiles(Path.Combine(RepoRoot(), "config", "hooks"), "*.sh", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal)];

    private static IEnumerable<HookScript> ScriptRows() => HookInventory.Scripts.Where(s => s.Source.StartsWith("config/hooks/", StringComparison.Ordinal));

    private static HookScript RowFor(string file) =>
        HookInventory.Scripts.Single(s => string.Equals(s.Source, "config/hooks/" + Path.GetFileName(file), StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void Hook_inventory_matches_config_hooks()
    {
        var installed = HookFiles().Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(installed, [.. ScriptRows().Select(s => s.Name).Order(StringComparer.Ordinal)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_inline_and_mod_rows_are_installed_where_they_say()
    {
        var settings = File.ReadAllText(Path.Combine(RepoRoot(), "config", "settings.json"));

        foreach (var row in HookInventory.Scripts.Where(s => !s.Source.StartsWith("config/hooks/", StringComparison.Ordinal)))
        {
            Assert.True(
                string.Equals(row.Source, "config/settings.json", StringComparison.Ordinal) ? settings.Contains("# aw:" + row.Name, StringComparison.Ordinal) : Directory.Exists(Path.Combine(RepoRoot(), row.Source)),
                $"{row.Name}: not found in {row.Source}");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_permission_answering_column_matches_what_the_hooks_print()
    {
        foreach (var file in HookFiles())
        {
            var answers = AllowDecision.IsMatch(File.ReadAllText(file));
            var entry = RowFor(file);

            Assert.True(entry.AnswersPermissions == answers, $"{entry.Name}: inventory says {entry.AnswersPermissions}, the script says {answers}");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_rewrites_column_matches_what_the_hooks_print()
    {
        foreach (var file in HookFiles())
        {
            var rewrites = File.ReadAllText(file).Contains("updatedInput", StringComparison.Ordinal);
            var entry = RowFor(file);

            Assert.True(entry.RewritesInput == rewrites, $"{entry.Name}: inventory says {entry.RewritesInput}, the script says {rewrites}");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_state_path_the_hooks_use_is_in_the_inventory()
    {
        var files = HookFiles().Append(Path.Combine(RepoRoot(), "config", "lib", "failopen.sh")).ToList();
        var used = files.SelectMany(f => StatePath.Matches(File.ReadAllText(f)).Select(m => m.Groups["p"].Value.TrimEnd('/', '"', '}'))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(used);
        foreach (var path in used)
        {
            Assert.True(
                HookInventory.StatePaths.Any(e => string.Equals(path, e.Path, StringComparison.Ordinal) || path.StartsWith(e.Path + "/", StringComparison.Ordinal)),
                $"AW_STATE_DIR/{path} is used by a hook and is not in the inventory");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_inventory_binds_what_the_spec_names()
    {
        var kinds = HookInventory.StatePaths.ToDictionary(e => e.Path, e => e.Kind, StringComparer.Ordinal);

        Assert.Equal(StatePathKind.ReadWrite, kinds["judge"]);
        Assert.Equal(StatePathKind.ReadWrite, kinds["locks"]);
        Assert.Equal(StatePathKind.PerLaunchDir, kinds["logs"]);
        Assert.Equal(StatePathKind.PerLaunchDir, kinds["probe"]);
        Assert.Equal(StatePathKind.PerLaunchDir, kinds["digests"]);
        Assert.Equal(StatePathKind.ReadOnly, kinds["context-guard"]);
        Assert.Equal(StatePathKind.ReadWrite, kinds["context-guard/state"]);
        Assert.Equal(StatePathKind.ReadOnly, kinds["bin-r1b"]);
        Assert.Equal(StatePathKind.Absent, kinds["meta"]);
        Assert.Equal(StatePathKind.Absent, kinds["bin/aw-meta"]);
        Assert.All(HookInventory.StatePaths, e => Assert.False(string.IsNullOrWhiteSpace(e.UsedBy), e.Path));
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_a_hook_that_answers_allow_is_dropped_and_only_a_rewriting_hook_has_its_forms_denied()
    {
        Assert.All(HookInventory.Scripts.Where(s => s.Treatment == HookTreatment.Drop), s => Assert.True(s.AnswersPermissions, s.Name));
        Assert.All(HookInventory.Scripts.Where(s => s.Treatment == HookTreatment.KeepDenyRewrites), s => Assert.True(s.RewritesInput && s.RewritePrefix is { Length: > 0 }, s.Name));
        Assert.All(HookInventory.Scripts, s => Assert.False(string.IsNullOrWhiteSpace(s.Why), s.Name));
        Assert.Equal(HookInventory.Scripts.Count, HookInventory.Scripts.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [GeneratedRegex("permissionDecision\"?\\s*:\\s*\"allow\"", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AllowDecision { get; }

    [GeneratedRegex("(?:AW_STATE_DIR:-\\$HOME/\\.agentic-workflow\\}|\\$AW_STATE_DIR)/(?<p>[A-Za-z0-9_./-]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StatePath { get; }
}
