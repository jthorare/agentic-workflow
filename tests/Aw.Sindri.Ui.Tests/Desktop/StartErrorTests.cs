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
