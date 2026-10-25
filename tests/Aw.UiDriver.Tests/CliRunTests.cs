using Aw.UiDriver.Cli;

namespace Aw.UiDriver.Tests;

public sealed class CliRunTests
{
    private sealed record Ran(int Exit, string Output, string Error);

    private static async Task<Ran> Run(string? directory, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await new CliRun(output, error, name => name == "AW_UI_DRIVER_DIR" ? directory : null).RunAsync(args, TestContext.Current.CancellationToken);
        return new Ran(exit, output.ToString().Trim(), error.ToString().Trim());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_verb_is_posted_with_the_secret_and_the_answer_is_printed()
    {
        await using var driver = new StubDriver();
        driver.Answer = (200, "application/json", """{"node":{"id":"0/0"}}"""u8.ToArray());

        var ran = await Run(driver.Folder, "click", "--automation-id", "save");

        Assert.Equal((0, """{"node":{"id":"0/0"}}""", ""), (ran.Exit, ran.Output, ran.Error));
        var call = Assert.Single(driver.Calls);
        Assert.Equal(("/v1/click", StubDriver.Secret), (call.Path, call.Secret));
        Assert.Contains("\"automationId\":\"save\"", call.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_dir_flag_beats_the_environment()
    {
        await using var driver = new StubDriver();

        var ran = await Run("/nonexistent", "--dir", driver.Folder, "tree");

        Assert.Equal(0, ran.Exit);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(404, "AW-UIDRIVER-002", 2)]
    [InlineData(408, "AW-UIDRIVER-004", 2)]
    [InlineData(400, "AW-UIDRIVER-001", 1)]
    [InlineData(401, "AW-UIDRIVER-005", 4)]
    [InlineData(500, "AW-UIDRIVER-007", 4)]
    public async Task A_failure_is_one_line_on_stderr_and_an_exit_code_by_its_kind(int status, string code, int exit)
    {
        await using var driver = new StubDriver();
        driver.Fail(status, code, "it went wrong badly");

        var ran = await Run(driver.Folder, "click", "--name", "x");

        Assert.Equal((exit, "", $"aw-ui-driver: {code} it went wrong badly"), (ran.Exit, ran.Output, ran.Error));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_screenshot_is_written_to_the_file_it_is_asked_for()
    {
        await using var driver = new StubDriver();
        driver.Answer = (200, "image/png", [0x89, 0x50, 0x4E, 0x47]);
        var path = Path.Combine(driver.Folder, "shots", "a.png");

        var ran = await Run(driver.Folder, "screenshot", "--out", path);

        Assert.Equal(0, ran.Exit);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Contains("\"bytes\":4", ran.Output, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_screenshot_without_out_is_a_usage_error()
    {
        await using var driver = new StubDriver();
        driver.Answer = (200, "image/png", [1]);

        var ran = await Run(driver.Folder, "screenshot");

        Assert.Equal((1, "aw-ui-driver: a screenshot needs --out <file.png>"), (ran.Exit, ran.Error));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Ready_answers_once_the_driver_pings()
    {
        await using var driver = new StubDriver();

        var ran = await Run(driver.Folder, "ready", "--wait-ms", "2000");

        Assert.Equal((0, """{"ok":true}"""), (ran.Exit, ran.Output));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Ready_gives_up_with_unreachable_when_no_driver_is_there()
    {
        var empty = Directory.CreateTempSubdirectory("aw-uidriver-empty-");
        try
        {
            var ran = await Run(empty.FullName, "ready", "--wait-ms", "300");

            Assert.Equal(4, ran.Exit);
            Assert.StartsWith("aw-ui-driver: AW-UIDRIVER-006", ran.Error, StringComparison.Ordinal);
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_directory_or_a_usage_mistake_is_exit_1()
    {
        var none = await Run(directory: null, "tree");
        var usage = await Run("/d", "tree", "--bogus", "x");

        Assert.Equal((1, 1), (none.Exit, usage.Exit));
        Assert.Contains("no driver directory", none.Error, StringComparison.Ordinal);
        Assert.StartsWith("aw-ui-driver: usage:", usage.Error, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_driver_that_never_answers_is_a_timeout_not_a_hang()
    {
        await using var driver = new StubDriver { Silent = true };
        var error = new StringWriter();
        var run = new CliRun(new StringWriter(), error, name => name switch
        {
            "AW_UI_DRIVER_DIR" => driver.Folder,
            "AW_UI_DRIVER_TIMEOUT_MS" => "300",
            _ => null,
        });

        var exit = await run.RunAsync(["tree", "--depth", "80"], TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
        Assert.StartsWith("aw-ui-driver: AW-UIDRIVER-004 ", error.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task The_timeout_flag_beats_the_environment()
    {
        await using var driver = new StubDriver { Silent = true };
        var error = new StringWriter();
        var run = new CliRun(new StringWriter(), error, name => name switch
        {
            "AW_UI_DRIVER_DIR" => driver.Folder,
            "AW_UI_DRIVER_TIMEOUT_MS" => "600000",
            _ => null,
        });

        var exit = await run.RunAsync(["tree", "--timeout-ms", "300"], TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
        Assert.Contains("AW-UIDRIVER-004", error.ToString(), StringComparison.Ordinal);
    }
}
