using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

public sealed class UiLauncherTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_app_is_found_next_to_the_sindri_binary_by_default() =>
        Assert.Equal(Path.Combine("/opt/aw", "sindri-ui"), UiLauncher.Locate(_ => null, "/opt/aw"));

    [Fact, Trait("Lane", "unit")]
    public void AW_SINDRI_UI_names_another_app_binary() =>
        Assert.Equal("/elsewhere/sindri-ui", UiLauncher.Locate(n => n == UiLauncher.Variable ? "/elsewhere/sindri-ui" : null, "/opt/aw"));

    [Fact, Trait("Lane", "unit")]
    public void A_missing_app_says_where_it_looked_and_the_fix()
    {
        var line = UiLauncher.Open("/nonexistent/sindri-ui");

        Assert.Equal("not found at /nonexistent/sindri-ui (fix: publish Aw.Sindri.Ui.Desktop next to sindri, or set AW_SINDRI_UI); use sindri status", line);
    }

    [Fact, Trait("Lane", "integration")]
    public void An_app_that_exists_is_started_detached_and_named_with_its_pid()
    {
        var line = UiLauncher.Open("/usr/bin/true");

        Assert.Matches(@"^opened \(true, pid \d+\)$", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_relative_path_is_resolved_against_the_working_directory_before_anything_runs()
    {
        var line = UiLauncher.Open("no-such-dir/sindri-ui");

        Assert.StartsWith($"not found at {Path.GetFullPath("no-such-dir/sindri-ui")} ", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_file_that_isnt_executable_is_named_with_the_fix()
    {
        var dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;
        try
        {
            var file = Path.Combine(dir, "sindri-ui");
            File.WriteAllText(file, "not a program");
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            var line = UiLauncher.Open(file);

            Assert.StartsWith($"not executable at {file} (fix: ", line, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Headless_has_no_opener_and_bare_sindri_opens_the_located_app()
    {
        Assert.Null(CommandTable.UiOpener(headless: true, _ => "/nonexistent/sindri-ui"));

        var open = CommandTable.UiOpener(headless: false, n => n == UiLauncher.Variable ? "/nonexistent/sindri-ui" : null);

        Assert.NotNull(open);
        Assert.StartsWith("not found at /nonexistent/sindri-ui ", open(), StringComparison.Ordinal);
    }
}
