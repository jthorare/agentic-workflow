using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Host;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Tests.Contracts;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cutover;

/// <summary>
/// Cutover spec §4.3 (b) and (c): the judge adapter spawns `judge resolution-check` (the input JSON on stdin), then `judge why &lt;id&gt;`.
/// They run against a stub `judge` script. The two mismatches of (c) are here too: the digest the
/// answer names is the judge's 16-hex input digest, and the check kind `ui` goes out as `ui-evidence`.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JudgeCliTests : IDisposable
{

    // resolution-check: save stdin, print the canned envelope, exit as told. why: print the stored row, whose input_digest is the
    // first 16 hex of the SHA-256 of what was sent (the judge's canonical digest of a compact input).
    private static readonly string _script = string.Join('\n',
    [
        """#!/bin/bash""",
        """printf '%s\n' "$*" >> @DIR@/calls """,
        """env | grep '^AW_' > @DIR@/env || true """,
        """case "$1" in """,
        """  resolution-check) """,
        """    cat > @DIR@/stdin """,
        """    cat @DIR@/envelope.json """,
        """    exit "$(cat @DIR@/exit)" """,
        """    ;; """,
        """  why) """,
        """    digest="$(sha256sum @DIR@/stdin | cut -c1-16)" """,
        """    printf '{"id":"%s","question":"resolution-check","decision":"resolved","reason_code":"judged","input_digest":"%s","outcome":"decided"}\n' "$2" "$digest" """,
        """    ;; """,
        """  *) exit 64 ;; """,
        """esac """,
        "",
    ]);

    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(BugFixJsonContext.Default);
    private readonly StubBinary _stub = new("judge", _script);
    private readonly TempDirectory _work = new();

    public void Dispose()
    {
        _stub.Dispose();
        _work.Dispose();
    }

    [Fact]
    public void It_sends_the_input_on_stdin_to_resolution_check_then_asks_why_for_the_decision()
    {
        Decides("resolved");
        var question = Question(CheckKind.Test);

        var answer = Adapter(("AW_JUDGE_BIN", _stub.Path)).ResolutionCheck(question);

        Assert.True(answer.IsSuccess);
        Assert.Equal(["resolution-check", "why d-42"], _stub.Read("calls").Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(File.ReadAllText(question.InputFile), _stub.Read("stdin"));
    }

    [Theory]
    [InlineData("resolved", JudgeDecision.Resolved)]
    [InlineData("partial", JudgeDecision.Partial)]
    [InlineData("unresolved", JudgeDecision.Unresolved)]
    public void An_exit_0_envelope_is_the_decision_with_its_id_reason_code_and_reasons(string decision, JudgeDecision expected)
    {
        Decides(decision);

        var answer = Adapter(("AW_JUDGE_BIN", _stub.Path)).ResolutionCheck(Question(CheckKind.Test)).Value;

        Assert.Equal(expected, answer.Decision);
        Assert.Equal(JudgeDecisionId.From("d-42"), answer.DecisionId);
        Assert.Equal("judged", answer.ReasonCode);
        Assert.Equal(["the reported page now opens"], answer.Reasons);
    }

    [Fact]
    public void An_exit_2_envelope_is_an_escalation_without_a_decision_id_and_never_asks_why()
    {
        _stub.Set("exit", "2");
        _stub.Set("envelope.json", """{"escalated":true,"reasonCode":"below-threshold"}""");

        var answer = Adapter(("AW_JUDGE_BIN", _stub.Path)).ResolutionCheck(Question(CheckKind.Test)).Value;

        Assert.Equal(JudgeDecision.Escalated, answer.Decision);
        Assert.Null(answer.DecisionId);
        Assert.Equal("below-threshold", answer.ReasonCode);
        Assert.Equal(["resolution-check"], _stub.Read("calls").Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public void Any_other_exit_is_no_usable_verdict_AW_BUGFIX_016(int exit)
    {
        _stub.Set("exit", exit.ToString(CultureInfo.InvariantCulture));
        _stub.Set("envelope.json", string.Empty);

        var answer = Adapter(("AW_JUDGE_BIN", _stub.Path)).ResolutionCheck(Question(CheckKind.Test));

        Assert.Equal(BugFixErrors.JudgeUnavailable.Code, answer.Error.Code);
    }

    [Fact]
    public void Without_AW_JUDGE_BIN_the_binary_is_found_on_PATH()
    {
        Decides("resolved");

        var answer = Adapter(("PATH", _stub.Directory)).ResolutionCheck(Question(CheckKind.Test));

        Assert.True(answer.IsSuccess);
    }

    [Fact]
    public void A_missing_binary_is_AW_BUGFIX_016_naming_the_variable()
    {
        var answer = Adapter(("AW_JUDGE_BIN", Path.Combine(_work.Path, "no-such-judge"))).ResolutionCheck(Question(CheckKind.Test));

        Assert.Equal(BugFixErrors.JudgeUnavailable.Code, answer.Error.Code);
        Assert.Contains("AW_JUDGE_BIN", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_judge_reads_the_same_state_as_the_hooks_AW_JUDGE_STATE_DIR_over_AW_STATE_DIR()
    {
        Decides("resolved");

        _ = Adapter(("AW_JUDGE_BIN", _stub.Path), ("AW_STATE_DIR", "/state/aw")).ResolutionCheck(Question(CheckKind.Test));
        Assert.Contains("AW_STATE_DIR=/state/aw", _stub.Read("env"), StringComparison.Ordinal);

        _ = Adapter(("AW_JUDGE_BIN", _stub.Path), ("AW_STATE_DIR", "/state/aw"), ("AW_JUDGE_STATE_DIR", "/state/judge")).ResolutionCheck(Question(CheckKind.Test));
        Assert.Contains("AW_JUDGE_STATE_DIR=/state/judge", _stub.Read("env"), StringComparison.Ordinal);
    }

    // (c) mismatch 1: the digest. The port's InputSha256 is the judge's input digest (first 16 hex of the SHA-256 of the canonical compact
    // input), the adapter returns what `judge why` stored, and the helper compares the two.
    [Fact]
    public void The_input_digest_is_the_judges_16_hex_canonical_digest_not_a_64_hex_file_hash()
    {
        Decides("resolved");
        var question = Question(CheckKind.Test);

        var answer = Adapter(("AW_JUDGE_BIN", _stub.Path)).ResolutionCheck(question).Value;

        Assert.Matches("^[0-9a-f]{16}$", question.InputSha256);
        Assert.Equal(question.InputSha256, answer.InputSha256);
    }

    // (c) mismatch 2: the check kind. BugFix says `ui`, the judge's resolution-check takes `ui-evidence`.
    [Fact]
    public void The_check_kind_ui_goes_out_as_ui_evidence()
    {
        Decides("resolved");
        var question = Question(CheckKind.Ui);

        _ = Adapter(("AW_JUDGE_BIN", _stub.Path)).ResolutionCheck(question);

        Assert.Contains("\"checkKind\":\"ui-evidence\"", _stub.Read("stdin"), StringComparison.Ordinal);
        Assert.DoesNotContain("\"checkKind\":\"ui\"", _stub.Read("stdin"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_check_kind_test_goes_out_as_test()
    {
        Decides("resolved");

        _ = Adapter(("AW_JUDGE_BIN", _stub.Path)).ResolutionCheck(Question(CheckKind.Test));

        Assert.Contains("\"checkKind\":\"test\"", _stub.Read("stdin"), StringComparison.Ordinal);
    }

    // The ledger writes the file the judge will digest: compact, with the judge's check kind, named by the same 16-hex digest.
    [Theory]
    [InlineData(CheckKind.Test, "test")]
    [InlineData(CheckKind.Ui, "ui-evidence")]
    public void The_ledger_writes_the_judge_input_in_the_judges_canonical_form(CheckKind kind, string wireKind)
    {
        var question = Question(kind);

        var text = File.ReadAllText(question.InputFile);

        Assert.Contains($"\"checkKind\":\"{wireKind}\"", text, StringComparison.Ordinal);
        Assert.Equal(CanonicalDigest(text), question.InputSha256);
    }

    private static string CanonicalDigest(string text) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.Nodes.JsonNode.Parse(text)!.ToJsonString(_json))))[..16];

    private static JudgeCli Adapter(params (string Name, string Value)[] variables) => new(StubBinary.Environment(variables));

    private void Decides(string decision)
    {
        _stub.Set("exit", "0");
        _stub.Set("envelope.json", $$"""{"decision":"{{decision}}","confidence":0.9,"model":"jev","reasonCode":"judged","id":"d-42","reasons":["the reported page now opens"]}""" + "\n");
    }

    private JudgeQuestion Question(CheckKind kind)
    {
        var input = new ResolutionCheckInput("Login shows a blank page", "The dashboard opens", "A blank page", "The cookie is dropped.", kind, "the check", BeforePassed: false, AfterPassed: true, " 1 file changed", "diff --git a/x b/x");
        return new FileRunLedger(_json).WriteJudgeInput(_work.Path, CandidateId.From("c1"), input);
    }
}
