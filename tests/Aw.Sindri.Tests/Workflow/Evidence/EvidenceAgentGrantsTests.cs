using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Evidence;

namespace Aw.Sindri.Tests.Workflow.Evidence;

/// <summary>The evidence agent Steps run Sindri's 'ui-evidence' binary one verb at a time (cutover spec §3.1, §3.4): the exact grants, the briefs that name the verbs, and no flag the host guard holds.</summary>
[Lane(TestLane.Unit)]
public sealed partial class EvidenceAgentGrantsTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-00000000000000000000000000000001/evidence/1");
    private static readonly AgentStepTarget _target = new(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/app");
    private static readonly string[] _report = ["Bash(sindri workflow complete:*)", "Bash(sindri workflow status:*)", "Bash(sindri workflow events:*)"];

    private static readonly string[] _briefedSteps =
        ["step.evidence.ui-plan", "step.evidence.api-plan", "step.evidence.db-plan", "step.evidence.ui-run", "step.evidence.api-run", "step.evidence.db-run"];

    public static TheoryData<string, string[]> Grants => new()
    {
        { "step.evidence.ui-plan", ["Bash(ui-evidence doctor:*)", "Bash(ui-evidence provenance:*)"] },
        { "step.evidence.api-plan", ["Bash(ui-evidence doctor:*)"] },
        { "step.evidence.db-plan", ["Bash(ui-evidence doctor:*)"] },
        { "step.evidence.ui-run", ["Bash(ui-evidence doctor:*)", "Bash(ui-evidence run:*)", "Bash(ui-evidence parity:*)", "Bash(ui-evidence provenance:*)"] },
        { "step.evidence.api-run", ["Bash(ui-evidence doctor:*)", "Bash(ui-evidence api-run:*)"] },
        { "step.evidence.db-run", ["Bash(ui-evidence doctor:*)", "Bash(ui-evidence db-run:*)"] },
        {
            "step.evidence.publish",
            [
                "Bash(ui-evidence publish-plan:*)", "Bash(ui-evidence parity-plan:*)", "Bash(ui-evidence parity-comment:*)",
                "Bash(ui-evidence parity-put:*)", "Bash(ui-evidence parity-stale:*)", "Bash(ui-evidence gh-publish:*)",
            ]
        },
    };

    private static AgentStepRun Run(string block, bool writes = false) =>
        new(WorkflowInstanceId.Of(_address), _address, RunToken.From("1.1"), new StepRef("run", block), writes);

    private static IReadOnlyList<string> Tools(string block, bool writes = false) =>
        AgentLaunches.RequestFor(Run(block, writes), _target).Step!.Policy!.AllowedTools;

    private static string Brief(string block) =>
        string.Join('\n', EvidenceStepInstructions.All.Single(i => i.IsFor(BlockId.From(block))).Lines);

    [Theory, MemberData(nameof(Grants))]
    public void A_step_gets_exactly_the_verbs_it_names_one_grant_each(string block, string[] verbs) =>
        Assert.Equal([.. _report, .. verbs], Tools(block));

    [Fact]
    public void No_step_gets_the_blanket_grant_and_gh_publish_goes_to_the_publish_step_alone()
    {
        foreach (var block in _briefedSteps.Append("step.evidence.publish"))
        {
            Assert.DoesNotContain(Tools(block), static t => t == "Bash(ui-evidence:*)");
            Assert.Equal(block == "step.evidence.publish", Tools(block).Contains("Bash(ui-evidence gh-publish:*)", StringComparer.Ordinal));
        }
    }

    [Fact]
    public void A_write_scope_evidence_step_keeps_its_verbs_before_the_git_grants()
    {
        var tools = Tools("step.evidence.api-run", writes: true);

        Assert.Equal(["Bash(ui-evidence doctor:*)", "Bash(ui-evidence api-run:*)"], tools.Where(static t => t.StartsWith("Bash(ui-evidence", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Contains(tools, static t => t == "Bash(git commit:*)");
    }

    [Theory]
    [InlineData("step.build.green")]
    [InlineData("step.evidence.desk")]
    [InlineData("step.evidence.none")]
    public void A_step_that_is_not_an_evidence_agent_step_gets_no_ui_evidence_grant(string block) =>
        Assert.Equal(_report, Tools(block));

    [Theory]
    [InlineData("step.evidence.ui-plan")]
    [InlineData("step.evidence.api-plan")]
    [InlineData("step.evidence.db-plan")]
    [InlineData("step.evidence.ui-run")]
    [InlineData("step.evidence.api-run")]
    [InlineData("step.evidence.db-run")]
    public void Every_evidence_agent_step_has_a_brief_that_follows_the_skill(string block)
    {
        Assert.Contains("ui-evidence skill", Brief(block), StringComparison.Ordinal);
        Assert.NotEmpty(EvidenceGrants.VerbsFor(block));
    }

    [Fact]
    public void The_briefs_name_exactly_the_verbs_their_step_is_granted_and_each_is_a_verb_of_the_binary()
    {
        var binary = BinaryVerbs();
        foreach (var block in _briefedSteps)
        {
            var named = NamedVerbs(Brief(block)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

            Assert.Equal([.. EvidenceGrants.VerbsFor(block).Order(StringComparer.Ordinal)], named);
            Assert.All(named, verb => Assert.Contains(binary, v => v == verb));
        }
    }

    [Fact]
    public void No_brief_names_the_write_flag_or_a_host_that_is_not_loopback()
    {
        foreach (var block in _briefedSteps)
        {
            var brief = Brief(block);

            Assert.DoesNotContain("--allow-writes", brief, StringComparison.Ordinal);
            Assert.DoesNotContain("--allow-preview-host", brief, StringComparison.Ordinal);
            Assert.All(HostFlag.Matches(brief).Select(static m => m.Groups["host"].Value), host => Assert.Matches(@"^https?://(localhost|127\.0\.0\.1)(:\d+)?$", host));
        }
    }

    [Fact]
    public void No_brief_runs_a_check_with_anything_but_a_ui_evidence_verb()
    {
        foreach (var block in _briefedSteps)
        {
            var brief = Brief(block);

            Assert.DoesNotContain("node dist", brief, StringComparison.Ordinal);
            Assert.DoesNotContain("playwright", brief, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("with_stack_lock", brief, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_briefs_and_the_skill_name_the_same_verbs()
    {
        var skill = File.ReadAllText(Path.Combine(RepoRoot(), "skills", "ui-evidence", "SKILL.md"));
        var skillVerbs = NamedVerbs(skill).ToHashSet(StringComparer.Ordinal);

        Assert.All(_briefedSteps.SelectMany(static b => NamedVerbs(Brief(b))).Distinct(StringComparer.Ordinal), verb => Assert.Contains(skillVerbs, v => v == verb));
    }

    [GeneratedRegex(@"--host\s+(?<host>\S+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HostFlag { get; }

    [GeneratedRegex(@"`ui-evidence (?<verb>[a-z][a-z-]*)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Verb { get; }

    [GeneratedRegex("internal const string Name = \"(?<name>[a-z-]+)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommandName { get; }

    private static IEnumerable<string> NamedVerbs(string text) => Verb.Matches(text).Select(static m => m.Groups["verb"].Value);

    private static HashSet<string> BinaryVerbs() =>
        [.. Directory.GetFiles(Path.Combine(RepoRoot(), "src", "Evidence", "Aw.Evidence.Host", "Commands"), "*Command.cs")
            .Select(static f => CommandName.Match(File.ReadAllText(f)))
            .Where(static m => m.Success).Select(static m => m.Groups["name"].Value)];

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("no AgenticWorkflow.slnx above the test binaries");
    }
}
