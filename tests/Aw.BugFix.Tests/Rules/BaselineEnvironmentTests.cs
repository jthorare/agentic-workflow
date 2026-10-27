using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Tests.Rules;

/// <summary>A baseline frozen before a variable joined the default list still accepts later runs (#1154).</summary>
public sealed class BaselineEnvironmentTests
{
    private static readonly Dictionary<string, string> _baseline = new(StringComparer.Ordinal) { ["PATH"] = "/usr/bin", ["HOME"] = "/home/u" };

    [Fact, Trait("Lane", "unit")]
    public void A_baseline_without_DOTNET_ROOT_accepts_a_later_run_that_has_it()
    {
        var (check, run) = Pair();

        _ = World.Ok(check.Accepts(run with { Environment = new Dictionary<string, string>(_baseline, StringComparer.Ordinal) { ["DOTNET_ROOT"] = "digest" } }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_variable_the_baseline_recorded_is_still_compared()
    {
        var (check, run) = Pair();

        var changed = World.Refused(check.Accepts(run with { Environment = new Dictionary<string, string>(_baseline, StringComparer.Ordinal) { ["HOME"] = "/home/other" } }), BugFixErrors.RunRefused);
        var dropped = World.Refused(check.Accepts(run with { Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin" } }), BugFixErrors.RunRefused);

        Assert.Contains("different HOME", changed.Message, StringComparison.Ordinal);
        Assert.Contains("different HOME", dropped.Message, StringComparison.Ordinal);
    }

    private static (FrozenCheck Check, RunRecord Run) Pair() =>
        (new FrozenCheck(CheckKind.Test, "tests/bug.sh", "sha", ["bash", "tests/bug.sh"], Directory: string.Empty, ProgramSha256: "bash", Environment: _baseline, CommandFiles: []),
         new RunRecord(CheckKind.Test, RunOutcome.Failed, "c", Directory: string.Empty, "sha", ["bash", "tests/bug.sh"], ProgramSha256: "bash", Environment: _baseline, 1, "/log", Leftovers: new Dictionary<string, string>(StringComparer.Ordinal), Checkout: "/repo"));
}
