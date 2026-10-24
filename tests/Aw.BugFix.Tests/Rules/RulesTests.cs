using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Rules;

namespace Aw.BugFix.Tests.Rules;

public sealed class RulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_handoff_parser_reads_status_rows_files_and_the_root_cause_section()
    {
        const string markdown = "status: diagnosed\r\n\r\n## Hypotheses\r\n\r\n| # | Hypothesis | Cause-site files | Likelihood | Result |\r\n|---|---|---|---|---|\r\n| 1 | A | a.ts, b.ts | high | ruled-out |\r\n| 2 | B | `c d.ts` | low | confirmed |\r\nafter\r\n\r\n## Root Cause\r\n\r\nLine one.\r\nLine two.\r\n\r\n## Next\r\n";

        var handoff = World.Ok(HandoffParser.Parse(markdown));

        Assert.Equal("diagnosed", handoff.Status);
        Assert.Equal([["a.ts", "b.ts"], ["c d.ts"]], handoff.Hypotheses.Select(static h => h.Files));
        Assert.Equal([HypothesisResult.RuledOut, HypothesisResult.Confirmed], handoff.Hypotheses.Select(static h => h.Result));
        Assert.Equal("Line one.\nLine two.", handoff.RootCause);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("## Hypotheses\n", "no status: line")]
    [InlineData("status: diagnosed\n", "no ## Hypotheses section")]
    [InlineData("status: diagnosed\n## Hypotheses\n| # | h | f | l | r |\n|-|-|-|-|-|\n| x | h | f | l | confirmed |\n", "malformed hypotheses row")]
    [InlineData("status: diagnosed\n## Hypotheses\n| # | h | f | l | r |\n|-|-|-|-|-|\n| 1 | h | f | l | maybe |\n", "malformed hypotheses row")]
    public void The_handoff_parser_names_what_is_missing(string markdown, string reason)
    {
        var error = World.Refused(HandoffParser.Parse(markdown), BugFixErrors.HandoffRefused);

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/app.ts", "tests/bug.test.sh", false)]
    [InlineData("tests/bug.test.sh", "tests/bug.test.sh", true)]
    [InlineData("checks/helper.sh", "checks/bug.sh", true)]
    [InlineData("helper.sh", "bug.sh", false)]
    [InlineData("src/__snapshots__/a.snap", "t/bug.sh", true)]
    [InlineData("pkg/foo_test.go", "t/bug.sh", true)]
    [InlineData("Tests/LoginTest.java", "t/bug.sh", true)]
    [InlineData("src/Contest.cs", "t/bug.sh", false)]
    [InlineData("SRC/JEST.CONFIG.JS", "t/bug.sh", true)]
    [InlineData("web/tsconfig.app.json", "t/bug.sh", false)]
    [InlineData("scripts/run.sh", "t/bug.sh", true)]
    [InlineData("Makefile", "t/bug.sh", false)]
    [InlineData("build/rules.mk", "t/bug.sh", false)]
    [InlineData(".npmrc", "t/bug.sh", false)]
    [InlineData("justfile", "t/bug.sh", false)]
    [InlineData("src/makefile_parser.ts", "t/bug.sh", false)]
    [InlineData("package.json", "t/bug.sh", false)]
    [InlineData("Directory.Build.props", "t/bug.sh", false)]
    [InlineData("go.mod", "t/bug.sh", false)]
    [InlineData("conftest.py", "t/bug.sh", true)]
    [InlineData("pytest.ini", "t/bug.sh", true)]
    public void Protected_paths_cover_the_check_its_directory_tests_and_runner_config_but_not_build_files(string file, string check, bool touched) =>
        Assert.Equal(touched, ProtectedPaths.Touched([file], check, ["scripts/run.sh"]).Count == 1);

    [Theory, Trait("Lane", "unit")]
    [InlineData("bash t.sh", null)]
    [InlineData("npx vitest run t.test.ts", null)]
    [InlineData("python3.12 -m pytest t.py", null)]
    [InlineData("node --eval 1 t.js", "inline code")]
    [InlineData("python3.12 -c 1 t.py", "inline code")]
    [InlineData("/bin/cat t.sh", "not `cat`")]
    [InlineData("[ -f t.sh ]", "not `[`")]
    [InlineData("env sh t.sh", "not `env`")]
    [InlineData("timeout 60 bash t.sh", "not `timeout`")]
    [InlineData("node -p 1 t.js", "inline code (`node -p`)")]
    [InlineData("node --print 1 t.js", "inline code")]
    [InlineData("perl -E 1 t.pl", "inline code (`perl -E`)")]
    [InlineData("php -r 1 t.php", "inline code (`php -r`)")]
    [InlineData("deno eval 1 t.ts", "inline code (`deno eval`)")]
    [InlineData("npx -c true t.test.ts", "inline code (`npx -c`)")]
    [InlineData("npx jest t.test.ts", null)]
    [InlineData("npx node -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("npx --yes sh -c true t.sh", "inline code (`sh -c`)")]
    [InlineData("npm exec cat t.sh", "not `cat`")]
    [InlineData("uv run python -c 1 t.py", "inline code (`python -c`)")]
    [InlineData("npm test t.test.ts", null)]
    [InlineData("bash -e t.sh", null)]
    [InlineData("python -E t.py", null)]
    [InlineData("bash -xec true t.sh", "inline code (`bash -xec`)")]
    [InlineData("bun -e 1 t.ts", "inline code (`bun -e`)")]
    [InlineData("bun -e 1 t.ts x", "inline code (`bun -e`)")]
    [InlineData("bun -p 0 x t.ts", "inline code (`bun -p`)")]
    [InlineData("bun t.ts x", null)]
    [InlineData("bun test t.test.ts", null)]
    [InlineData("ruby -Itest t_test.rb", null)]
    [InlineData("perl -Ilib t.t", null)]
    [InlineData("ruby -e 1 t.rb", "inline code (`ruby -e`)")]
    [InlineData("ruby -We 1 t.rb", "inline code (`ruby -We`)")]
    [InlineData("perl -We 1 t.t", "inline code (`perl -We`)")]
    [InlineData("perl -le 1 t.t", "inline code (`perl -le`)")]
    [InlineData("perl -Mstrict t.t", null)]
    [InlineData("npx --registry https://r node -e 1 t.js", "option `--registry`")]
    [InlineData("npx -p pkg -c true t.js", "inline code (`npx -c`)")]
    [InlineData("npx -p pkg grep -q keep t.js", "not `grep`")]
    [InlineData("pnpm grep -q keep t.js", "not `grep`")]
    [InlineData("uv tool run grep t.py", "not `grep`")]
    [InlineData("uvx grep t.py", "not `grep`")]
    [InlineData("npm run test:unit t.js", null)]
    [InlineData("npm --node-options=--require=./x.js exec vitest t.js", "option `--node-options`")]
    [InlineData("uv run --env-file .env pytest t.py", "option `--env-file`")]
    [InlineData("pnpm --filter x exec node -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("uv run --index-url https://x python -c 1 t.py", "option `--index-url`")]
    [InlineData("npx playwright test tests/x.spec.ts", null)]
    [InlineData("pnpm exec vitest run t.test.ts", null)]
    [InlineData("npx -p vitest node -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("uv run --with pytest python -c 1 t.py", "inline code (`python -c`)")]
    [InlineData("yarn node -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("python -m pytest -c pytest.ini t.py", null)]
    [InlineData("python t.py -c x", null)]
    [InlineData("python -X dev -c 1 t.py", "inline code (`python -c`)")]
    [InlineData("bash -o pipefail -c true t.sh", "inline code (`bash -c`)")]
    [InlineData("bash t.sh -c", null)]
    [InlineData("node -r ./x.js -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("node t.js -e", null)]
    [InlineData("pnpm --filter web test t.js", null)]
    [InlineData("npm -w web test t.js", null)]
    [InlineData("uv run -m pytest t.py", null)]
    [InlineData("bun x -p vitest vitest t.ts", null)]
    [InlineData("python3 --check-hash-based-pycs always -c 1 t.py", "inline code (`python3 -c`)")]
    [InlineData("node --redirect-warnings w.txt -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("node --test-reporter spec -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("npx -y node --redirect-warnings w.txt -e 1 t.js", "inline code (`node -e`)")]
    [InlineData("php -d k=v -r 1 t.php", "inline code (`php -r`)")]
    [InlineData("bash --rcfile f -c true t.sh", "inline code (`bash -c`)")]
    [InlineData("pnpm -w grep -q x t.js", "not `grep`")]
    [InlineData("npm -w web exec vitest t.js", null)]
    [InlineData("python -B -m pytest -c pytest.ini t.py", null)]
    public void The_check_command_rule_refuses_non_runners_and_inline_code(string command, string? reason)
    {
        var refusal = CheckCommandRule.Refusal(command.Split(' '));

        if (reason is null)
        {
            Assert.Null(refusal);
        }
        else
        {
            Assert.Contains(reason, refusal, StringComparison.Ordinal);
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { RunOutcome.Passed, RunOutcome.Passed }, RunOutcome.Passed)]
    [InlineData(new[] { RunOutcome.Broken, RunOutcome.Failed }, RunOutcome.Failed)]
    [InlineData(new[] { RunOutcome.Passed, RunOutcome.Broken }, RunOutcome.Broken)]
    [InlineData(new RunOutcome[0], RunOutcome.Broken)]
    public void A_ui_run_fails_on_any_failed_step_and_is_broken_on_broken_steps_alone(RunOutcome[] steps, RunOutcome expected) =>
        Assert.Equal(expected, new UiRun(steps, "sha", 0).Outcome);

    [Fact, Trait("Lane", "unit")]
    public void A_frozen_test_check_accepts_only_the_same_command_in_the_same_directory()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin", ["HOME"] = "/home/u" };
        var check = new FrozenCheck(CheckKind.Test, "tests/bug.sh", "sha", ["bash", "tests/bug.sh"], Directory: string.Empty, ProgramSha256: "bash", Environment: env, CommandFiles: []);
        var run = new RunRecord(CheckKind.Test, RunOutcome.Passed, "c", Directory: string.Empty, "sha", ["bash", "tests/bug.sh"], ProgramSha256: "bash", Environment: env, 0, "/log", Leftovers: new Dictionary<string, string>(StringComparer.Ordinal), Checkout: "/repo");

        _ = World.Ok(check.Accepts(run));
        _ = World.Refused(check.Accepts(run with { Directory = "lib" }), BugFixErrors.RunRefused);
        _ = World.Refused(check.Accepts(run with { Command = ["bash", "./tests/bug.sh"] }), BugFixErrors.RunRefused);
        _ = World.Refused(check.Accepts(run with { Outcome = RunOutcome.Broken }), BugFixErrors.RunRefused);
        _ = World.Refused(check.Accepts(run with { ProgramSha256 = "another bash" }), BugFixErrors.RunRefused);
        var otherPath = World.Refused(check.Accepts(run with { Environment = new Dictionary<string, string>(env, StringComparer.Ordinal) { ["PATH"] = "/evil:/usr/bin" } }), BugFixErrors.RunRefused);
        Assert.Contains("different PATH", otherPath.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Command_files_are_positional_paths_config_files_and_module_specifiers()
    {
        var paths = CommandFilesRule.Paths([
            "node", "--require=./lib/setup", "-r./lib/r", "--config=vt.ts", "--outputFile=reports/junit.xml", "--test-name-pattern=/x/",
            "--base-url=http://localhost:3000", "--cov=app", "-m", "tools.runner", "./pkg/...", "tests/t.js",
        ]).ToDictionary(static p => p.Path, static p => (p.Required, p.Specifier), StringComparer.Ordinal);

        Assert.Equal((true, true), paths["./lib/setup"]);
        Assert.Equal((true, true), paths["./lib/r"]);
        Assert.Equal((true, false), paths["vt.ts"]);
        Assert.Equal((false, false), paths["reports/junit.xml"]);
        Assert.Equal((false, false), paths["/x/"]);
        Assert.Equal((false, false), paths["http://localhost:3000"]);
        Assert.Equal((false, false), paths["app"]);
        Assert.Equal((false, true), paths["tools/runner.py"]);
        Assert.Equal((false, true), paths["tools/__main__.py"]);
        Assert.Equal((false, false), paths["./pkg/..."]);
        Assert.Equal((true, false), paths["tests/t.js"]);
        Assert.Equal(["./lib/setup", "./lib/setup.js", "./lib/setup.cjs"], CommandFilesRule.LoaderCandidates("./lib/setup").Take(3), StringComparer.Ordinal);
    }
}
