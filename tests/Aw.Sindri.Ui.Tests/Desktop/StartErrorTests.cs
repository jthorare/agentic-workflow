using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The words the Didn't start card shows: serve's last error line peeled to its innermost reason (#3326, qa).</summary>
[Lane(TestLane.Unit)]
public sealed class StartErrorTests
{
    [Fact]
    public void The_real_line_keeps_only_the_innermost_reason()
    {
        var reason = StartError.Reason("SND-CLI-002 'sindri serve' exited: SND-API-003 Failed to bind to address http://127.0.0.1:32791: address already in use. (fix: free the port, or set AW_SINDRI_PORT) (log: /tmp/s3326/logs/serve.log)");

        Assert.Equal("Failed to bind to address http://127.0.0.1:32791: address already in use. (fix: free the port, or set AW_SINDRI_PORT)", reason);
    }

    [Fact]
    public void The_fix_hint_stays_and_only_the_log_part_goes()
    {
        var reason = StartError.Reason("SND-API-003 address already in use. (fix: free the port, or set AW_SINDRI_PORT) (log: /x/serve.log)");

        Assert.Contains("(fix: free the port, or set AW_SINDRI_PORT)", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("(log:", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_plain_exception_line_comes_back_unchanged()
    {
        Assert.Equal("System.IO.IOException: disk full", StartError.Reason("System.IO.IOException: disk full"));
    }

    [Fact]
    public void A_line_with_no_codes_only_loses_its_log_path()
    {
        Assert.Equal("disk is full", StartError.Reason("disk is full (log: /x/serve.log)"));
    }

    [Fact]
    public void A_line_with_no_log_part_only_loses_its_codes()
    {
        Assert.Equal("address already in use", StartError.Reason("SND-API-003 address already in use"));
    }

    [Fact]
    public void No_line_reads_as_no_error_line()
    {
        Assert.Equal(Labels.NoErrorLine, StartError.Reason(null));
    }
}
