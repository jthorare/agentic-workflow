using System.Xml.Linq;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>
/// ARCH-SLACKFAKE-001 (messages spec §8.1, M1a): the fake Slack's loopback host, tests/Aw.Connectors.Testing.Server, is test support. No project under src/
/// references it, so production never ships a Kestrel that answers as Slack.
/// </summary>
[Trait("Lane", "integration")]
public sealed class FakeSlackHostReferenceTests
{
    private const string RuleId = "ARCH-SLACKFAKE-001";
    private const string HostProject = "Aw.Connectors.Testing.Server";

    [Fact]
    public void Nothing_under_src_references_the_fake_slack_host()
    {
        var source = Path.Combine(Solution.RepoRoot, "src");
        var projects = Directory.EnumerateFiles(source, "*.csproj", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(projects);   // why: the rule must not pass for want of projects
        Assert.True(File.Exists(Path.Combine(Solution.RepoRoot, "tests", HostProject, HostProject + ".csproj")));

        Verify.None(projects.SelectMany(static p => XDocument.Load(p).Descendants("ProjectReference")
            .Select(r => (Project: Path.GetRelativePath(Solution.RepoRoot, p).Replace('\\', '/'), Reference: (string?)r.Attribute("Include") ?? string.Empty)))
            .Where(static r => Path.GetFileNameWithoutExtension(r.Reference.Replace('\\', '/')) == HostProject)
            .Select(static r => new Violation(RuleId, $"{r.Project} references {r.Reference}", "production reads Slack through its ports; only test code starts the fake's host")));
    }
}
