using Aw.Kernel.Testing;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.AdoptToolkits;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Tests.BuildToolkit;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>Adopt on serve start (agent-first PR 16c): a toolkit folder under the toolkits root with no row gets one in state 'off', so a folder an agent made by hand can be built.</summary>
[Lane(TestLane.Unit)]
public sealed class ToolkitAdoptionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_folder_with_no_row_gets_an_off_row_created_by_adopted_and_a_folder_with_a_row_is_left_alone()
    {
        var store = new ToolkitRig.FakeStore();
        var folders = new ToolkitRig.FakeFolders();
        var kept = new Toolkit("known", "/tk/known", ToolkitState.Built, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, Manifest: null, CreatedBy: "human");
        store.Items["known"] = kept;
        folders.Existing.AddRange([new ToolkitFolder("known", "/tk/known"), new ToolkitFolder("loose", "/tk/loose")]);

        var adopted = await new ToolkitAdoption(store, folders).Run(Token);

        Assert.Equal(1, adopted.Value);
        Assert.Same(kept, store.Items["known"]);
        Assert.Equal(new Toolkit("loose", "/tk/loose", ToolkitState.Off, SourceHash: null, BuiltAt: null, Manifest: null, CreatedBy: "adopted"), store.Items["loose"]);
    }

    [Theory]
    [InlineData("Has_Underscore")]
    [InlineData("UpperCase")]
    [InlineData(".hidden")]
    public async Task A_folder_whose_name_can_not_be_a_toolkit_name_is_not_adopted(string folder)
    {
        var store = new ToolkitRig.FakeStore();
        var folders = new ToolkitRig.FakeFolders();
        folders.Existing.Add(new ToolkitFolder(folder, "/tk/" + folder));

        var adopted = await new ToolkitAdoption(store, folders).Run(Token);

        Assert.Equal(0, adopted.Value);
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task A_second_pass_adds_nothing()
    {
        var store = new ToolkitRig.FakeStore();
        var folders = new ToolkitRig.FakeFolders();
        folders.Existing.Add(new ToolkitFolder("loose", "/tk/loose"));
        var adoption = new ToolkitAdoption(store, folders);

        _ = await adoption.Run(Token);
        var again = await adoption.Run(Token);

        Assert.Equal(0, again.Value);
    }

    [Fact]
    public async Task The_host_adopts_once_at_start_and_finishes()
    {
        var store = new ToolkitRig.FakeStore();
        var folders = new ToolkitRig.FakeFolders();
        folders.Existing.Add(new ToolkitFolder("loose", "/tk/loose"));
        using var host = new ToolkitAdoptionHost(new ToolkitAdoption(store, folders), TimeProvider.System, new ListLogger<ToolkitAdoptionHost>());

        await host.StartAsync(Token);
        await host.ExecuteTask!.WaitAsync(Token);

        Assert.Equal(ToolkitState.Off, store.Items["loose"].State);
    }
}
