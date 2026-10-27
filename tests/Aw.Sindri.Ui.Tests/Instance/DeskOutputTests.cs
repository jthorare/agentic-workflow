using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>A desk task's required outputs (ux-A-1b): the sheet asks for each as a path box and Browse, Complete waits for them, and the report carries their contents.</summary>
[Lane(TestLane.Unit)]
public sealed class DeskOutputTests
{
    private const string Instance = "WFI-0000000000000000000000000000000a";
    private const string Key = "fact:output";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly Dictionary<string, PickedFile?> _disk = new(StringComparer.Ordinal);

    // The task the sheet reads before it sends (DeskReporter.Load reads the instance by id): the dev-desk-round-trip golden, whose desk requires fact:output.
    public DeskOutputTests()
    {
        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"));
    }

    private Task<PickedFile?> Read(string path, int limit, CancellationToken token) => Task.FromResult(_disk.GetValueOrDefault(path));

    private ExpandedStateViewModel Expanded()
    {
        var detail = FixtureInstances.Detail("dev-desk-round-trip");
        return new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock, readPath: Read)
        {
            Commander = new InstanceCommander(_server, () => _api),
            Desk = new DeskReporter(_server, () => _api),
            InstanceId = Instance,
        };
    }

    [Fact]
    public void A_required_output_is_an_input_and_Complete_waits_for_it()
    {
        var expanded = Expanded();

        var complete = expanded.Commands.Single(c => c.IsComplete);

        Assert.Equal(Key, Assert.Single(expanded.Outputs).Key);
        Assert.Equal((false, Labels.PickOutput(Key)), (complete.CanSend, expanded.OutputHint));

        expanded.Outputs[0].Path = "export.txt";

        Assert.Equal((true, string.Empty), (complete.CanSend, expanded.OutputHint));
    }

    [Fact]
    public async Task Complete_carries_the_contents_of_the_file_under_the_outputs_key()
    {
        _disk["export.txt"] = new PickedFile("export.txt", 3, "abc"u8.ToArray());
        var expanded = Expanded();
        expanded.Outputs[0].Path = "export.txt";

        await expanded.CompleteAsync();

        var (_, body) = Assert.Single(_server.CompleteCalls);
        Assert.Equal("abc"u8.ToArray(), body.Outputs[Key]);
        Assert.Equal(Labels.CompleteSent, expanded.PendingNote);
    }

    [Fact]
    public async Task A_file_over_1_MiB_is_refused_with_its_reason_and_nothing_is_sent()
    {
        _disk["big.bin"] = new PickedFile("big.bin", DeskOutputViewModel.MaxBytes + 1, ReadOnlyMemory<byte>.Empty);
        var expanded = Expanded();
        expanded.Outputs[0].Path = "big.bin";

        await expanded.CompleteAsync();

        Assert.Empty(_server.CompleteCalls);
        Assert.Equal(Labels.OutputTooLarge(Key), expanded.Outputs[0].Error);
    }

    [Fact]
    public async Task A_path_that_cannot_be_read_says_so_and_nothing_is_sent()
    {
        var expanded = Expanded();
        expanded.Outputs[0].Path = "missing.txt";

        await expanded.CompleteAsync();

        Assert.Empty(_server.CompleteCalls);
        Assert.Equal(Labels.OutputUnreadable("missing.txt"), expanded.Outputs[0].Error);
    }

    [Fact]
    public async Task Complete_without_the_output_sends_nothing_and_says_which_to_pick()
    {
        var expanded = Expanded();

        await expanded.CompleteAsync();

        Assert.Empty(_server.CompleteCalls);
        Assert.Equal(Labels.PickOutput(Key), expanded.PendingNote);
    }

    [Fact]
    public async Task Browse_keeps_the_chosen_file_and_sends_its_contents()
    {
        var picker = new FakeFilePicker();
        picker.Queue(new PickedFile("chosen.txt", 2, "hi"u8.ToArray()));
        var detail = FixtureInstances.Detail("dev-desk-round-trip");
        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock, files: picker)
        {
            Desk = new DeskReporter(_server, () => _api),
            InstanceId = Instance,
        };

        await expanded.Outputs[0].Browse(CancellationToken.None);
        await expanded.CompleteAsync();

        Assert.Equal("chosen.txt", expanded.Outputs[0].Path);
        Assert.Equal("hi"u8.ToArray(), Assert.Single(_server.CompleteCalls).Body.Outputs[Key]);
    }
}
