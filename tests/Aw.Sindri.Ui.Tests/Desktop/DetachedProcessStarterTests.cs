using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>What the detached start hands the child and what it does when the OS refuses (#3643, ideas review).</summary>
[Lane(TestLane.Unit)]
public sealed class DetachedProcessStarterTests
{
    [Fact]
    public void Scrub_drops_the_UI_driver_variables_and_keeps_the_rest()
    {
        var info = new ProcessStartInfo("/bin/sh");
        info.Environment["AW_UI_DRIVER"] = "1";
        info.Environment["AW_UI_DRIVER_DIR"] = "/x";
        info.Environment["AW_STATE_DIR"] = "/s";

        var scrubbed = DetachedProcessStarter.Scrub(info);

        Assert.DoesNotContain(scrubbed.Environment.Keys, static key => key.StartsWith("AW_UI_DRIVER", StringComparison.Ordinal));
        Assert.Equal("/s", scrubbed.Environment["AW_STATE_DIR"]);
    }

    [Fact]
    public void The_log_path_goes_to_the_child_as_AW_START_LOG()
    {
        var info = DetachedProcessStarter.Info(new ProcessLaunch("/x/sindri", ["--headless"], "/h/logs/serve.log", Number: 1));

        Assert.Equal("/h/logs/serve.log", info.Environment["AW_START_LOG"]);
        Assert.Contains("\"$AW_START_LOG\"", info.ArgumentList[1], StringComparison.Ordinal);
        Assert.DoesNotContain("\"$LOG\"", info.ArgumentList[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_spawn_that_throws_InvalidOperationException_is_a_failed_start_not_a_crash()
    {
        var starter = new DetachedProcessStarter(static _ => throw new InvalidOperationException("the process is gone"));

        var result = starter.Start(new ProcessLaunch("/x/sindri", ["--headless"], Path.Combine(Path.GetTempPath(), "sindri-start-test", "logs", "serve.log")));

        Assert.True(result.IsError);
    }
}
