using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>
/// Messages (messages spec §8.1, §14 M1s): Aw.Sindri.Core.Messages is pure. Its types use the BCL, Aw.Kernel and each other only, so the triage rules,
/// the rank and mrkdwn never reach the ledger, Home, the store or a Slack client. A phase that needs another Core namespace widens the list here, reviewed.
/// </summary>
[Trait("Lane", "integration")]
public sealed class MessagesCoreTests
{
    private const string RuleId = "ARCH-MESSAGES-001";
    private const string Namespace = "Aw.Sindri.Core.Messages";

    private static readonly string[] _allowed = ["System.", "Microsoft.CodeAnalysis.", "Aw.Kernel.", Namespace + "."];

    [Fact]
    public void Core_messages_uses_only_the_bcl_the_kernel_and_itself()
    {
        var edges = Production.Edges.Where(static e => TypeNames.NamespaceOf(e.Origin) == Namespace).ToList();

        Assert.NotEmpty(edges);   // why: the rule must not pass for want of the namespace
        Verify.None(edges.Where(static e => !_allowed.Any(a => e.Target.FullName.StartsWith(a, StringComparison.Ordinal)))
            .Select(static e => new Violation(RuleId, e.Text, "keep Core.Messages pure: move the dependency behind a port, or widen this rule's list in review")));
    }
}
