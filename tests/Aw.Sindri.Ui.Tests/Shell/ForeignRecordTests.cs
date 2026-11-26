using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// A control.json that names an address off this machine (hand-edited, or synced from another machine) is refused as an
/// unreadable record, with the reason and the record's path; the app never calls that address.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ForeignRecordTests : IAsyncDisposable
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FollowLoop _follow = new();
    private readonly HealthViewModel _vm;

    public ForeignRecordTests()
    {
        _vm = new HealthViewModel(_records, _server, _clock, new FakeClipboard());
    }

    public ValueTask DisposeAsync() => _follow.DisposeAsync();

    [Theory]
    [InlineData("http://example.com:7410/", "example.com:7410")]
    [InlineData("http://10.0.0.5:7410/", "10.0.0.5:7410")]
    public async Task A_record_off_this_machine_reads_as_unreadable_with_the_reason_and_the_path(string url, string authority)
    {
        _records.WriteText($$"""{"url":"{{url}}","pid":4242,"epoch":7}""");
        _follow.Start(_vm.Run);

        for (var i = 0; i < 200 && _vm.Headline != "Not running"; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("Not running", _vm.Headline);
        Assert.Equal(
            ($"The endpoint record can't be read: its address {authority} isn't on this machine (the app follows only 127.0.0.1, ::1 or localhost)", "memory:control.json"),
            (_vm.Reason, _vm.ReasonDetail));
    }
}
