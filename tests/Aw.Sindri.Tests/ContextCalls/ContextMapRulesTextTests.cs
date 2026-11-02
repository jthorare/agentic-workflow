using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Issue 1041: `context map status` names a map built under older rules, in words, and says nothing otherwise.</summary>
public sealed class ContextMapRulesTextTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private static MapStatusView View(bool rulesOlder) =>
        new(new MapVersion(1, _repo, Commit: null, KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Current), new MapCounts(13, 8, 0, 0, 0), Orphaned: 0, Retained: 0, ScopedTo: null) { RulesOlder = rulesOlder };

    [Fact, Trait("Lane", "unit")]
    public void Older_rules_get_one_plain_line_naming_the_fix() =>
        Assert.Contains("built under older map rules; a refresh will rebuild it (sindri context map refresh)\n", ContextMapText.Status(View(rulesOlder: true)), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Current_rules_add_no_line() =>
        Assert.DoesNotContain("older map rules", ContextMapText.Status(View(rulesOlder: false)), StringComparison.Ordinal);
}
