using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The sandbox launch codes -014 to -021, their kinds and reasons (sandbox spec SB11, §6; SB-0).</summary>
public sealed class SandboxCodesTests
{
    private static readonly string[] _numbers = ["014", "015", "016", "017", "018", "019", "020", "021"];

    [Fact, Trait("Lane", "unit")]
    public void The_eight_codes_are_unique_in_All_and_pass_the_catalogue_check()
    {
        var codes = _numbers.Select(n => "SND-LAUNCH-" + n).ToList();
        var present = SindriErrors.All.Select(e => e.Code).ToList();

        Assert.All(codes, c => Assert.Contains(c, present, StringComparer.Ordinal));
        Assert.Equal(present.Count, present.Distinct(StringComparer.Ordinal).Count());
        Assert.True(ErrorCatalogue.Validate(SindriErrors.All).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Code_kinds_match_the_engine_table()
    {
        var table = SandboxGoldens.Object("code-kinds.json")["codes"]!.AsArray();

        Assert.Equal(_numbers.Select(n => "SND-LAUNCH-" + n), table.Select(r => (string)r!["code"]!), StringComparer.Ordinal);
        foreach (var row in table)
        {
            var code = (string)row!["code"]!;
            var kind = SandboxCodes.All.Single(c => string.Equals(c.Error.Code, code, StringComparison.Ordinal));

            Assert.Equal((string)row["kind"]!, JsonName(kind.Kind));
            Assert.Equal(kind.HasHttpClass, row["http"] is not null);
            Assert.Equal(kind.Kind, SandboxCodes.KindOf(code));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Http_classes_are_the_hosts_mapping_of_the_error_kind()
    {
        var table = SandboxGoldens.Object("code-kinds.json")["codes"]!.AsArray();

        foreach (var row in table.Where(r => r!["http"] is not null))
        {
            var kind = SandboxCodes.All.Single(c => string.Equals(c.Error.Code, (string)row!["code"]!, StringComparison.Ordinal)).Error.Kind;

            Assert.Equal((int)row!["http"]!, HttpResults.StatusOf(kind));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_limit_code_has_no_http_class() =>
        Assert.Equal(["SND-LAUNCH-019"], SandboxCodes.All.Where(c => !c.HasHttpClass).Select(c => c.Error.Code), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_failure_reason_is_data_on_the_step_failure_codes_only()
    {
        var table = SandboxGoldens.Object("code-kinds.json")["codes"]!.AsArray();

        foreach (var row in table)
        {
            var code = SandboxCodes.All.Single(c => string.Equals(c.Error.Code, (string)row!["code"]!, StringComparison.Ordinal));

            Assert.Equal((string?)row!["failureReason"], code.FailureReason);
            Assert.Equal(code.Kind == LaunchCodeKind.StepFailure, code.FailureReason is not null);
        }

        Assert.Equal("limit", SandboxCodes.All.Single(c => c.Error.Code == "SND-LAUNCH-019").FailureReason);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_golden_reasons_are_the_defined_reasons()
    {
        var table = SandboxGoldens.Object("code-kinds.json")["codes"]!.AsArray();
        var golden = table.SelectMany(r => r!["reasons"]!.AsArray().Select(x => ((string)r["code"]!, (string)x!))).ToList();

        Assert.Equal(SandboxReasons.All.OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.Reason, StringComparer.Ordinal),
            golden.OrderBy(p => p.Item1, StringComparer.Ordinal).ThenBy(p => p.Item2, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_code_has_no_kind() => Assert.Null(SandboxCodes.KindOf("SND-LAUNCH-013"));

    [Fact, Trait("Lane", "unit")]
    public void A_refusal_carries_its_reason_and_source()
    {
        var error = SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.Expired, source: ModelLoginSource.SeparateLogin);

        Assert.Equal("SND-LAUNCH-017", error.Code);
        Assert.Equal("expired", error.Extensions!.Single(e => e.Key == "reason").Value.GetString());
        Assert.Equal("separate-login", error.Extensions!.Single(e => e.Key == "source").Value.GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_github_refusal_takes_any_three_digit_status()
    {
        Assert.Equal("SND-LAUNCH-018", SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, "http-404").Code);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, "http-4"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reason_of_another_code_is_refused() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.Expired));

    private static string JsonName(LaunchCodeKind kind) => kind switch
    {
        LaunchCodeKind.NoRuntime => "no-runtime",
        LaunchCodeKind.LaunchPark => "launch-park",
        LaunchCodeKind.StepFailure => "step-failure",
        LaunchCodeKind.ResourceWait => "resource-wait",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
