namespace Aw.Sindri.Tests.OnyxPurge;

// Owned-index spec §8.2 step 5 / U30: `sindri onyx down --purge` lists what an old `onyx up` left, asks the human, then deletes it in order.
public sealed class OnyxPurgeCommandTests : IDisposable
{
    private const string Label = OnyxPurgeRig.Label;
    private const string State = $$"""{"port":3000,"steps":{},"project":"{{Label}}","schemaVersion":1,"keyPointer":"keychain:onyx/sindri-onyx"}""";

    private readonly OnyxPurgeRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private void Seed()
    {
        _rig.WriteState(State);
        _rig.Docker.Containers[$"{Label}-api-1"] = Label;
        _rig.Docker.Containers[$"{Label}-db-1"] = Label;
        _rig.Docker.Containers[$"{Label}-lookalike-1"] = null;   // same prefix, no label: not Onyx's
        _rig.Docker.Volumes[$"{Label}_db"] = Label;
        _rig.Docker.Volumes[$"{Label}_unlabelled"] = null;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_list_names_only_labelled_containers_and_volumes()
    {
        Seed();

        var exit = await _rig.Run(human: true, answer: "no");

        Assert.Equal(0, exit);
        Assert.Contains($"{Label}-api-1", _rig.Out, StringComparison.Ordinal);
        Assert.Contains($"{Label}-db-1", _rig.Out, StringComparison.Ordinal);
        Assert.Contains($"{Label}_db", _rig.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("lookalike", _rig.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("unlabelled", _rig.Out, StringComparison.Ordinal);
        Assert.Contains("keychain:onyx/sindri-onyx", _rig.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unlabelled_look_alike_survives_a_yes()
    {
        Seed();

        var exit = await _rig.Run(human: true, answer: "yes");

        Assert.Equal(0, exit);
        Assert.Equal([$"{Label}-lookalike-1"], _rig.Docker.Containers.Keys, StringComparer.Ordinal);
        Assert.Equal([$"{Label}_unlabelled"], _rig.Docker.Volumes.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_agent_session_is_refused_and_nothing_is_listed_or_deleted()
    {
        Seed();

        var exit = await _rig.Run(human: false, answer: "yes");

        Assert.Equal(2, exit);
        Assert.Contains("SND-AUTH-002", _rig.Error, StringComparison.Ordinal);
        Assert.Empty(_rig.Calls);
        Assert.True(Directory.Exists(_rig.OnyxDirectory));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("no")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Anything_but_a_yes_deletes_nothing(string? answer)
    {
        Seed();

        var exit = await _rig.Run(human: true, answer);

        Assert.Equal(0, exit);
        Assert.Empty(_rig.Calls);
        Assert.True(File.Exists(Path.Combine(_rig.OnyxDirectory, ".env")));
        Assert.Equal(3, _rig.Docker.Containers.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Yes_deletes_containers_then_volumes_then_the_keychain_entry_then_the_directory()
    {
        Seed();

        var exit = await _rig.Run(human: true, answer: "y");

        Assert.Equal(0, exit);
        Assert.Equal([$"rm {Label}-api-1", $"rm {Label}-db-1", $"volume rm {Label}_db", "keychain onyx/sindri-onyx"], _rig.Calls);
        Assert.False(Directory.Exists(_rig.OnyxDirectory));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_state_and_nothing_labelled_says_nothing_is_left()
    {
        var exit = await _rig.Run(human: true, answer: null);

        Assert.Equal(0, exit);
        Assert.Contains("nothing left", _rig.Out, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_rig.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_pointer_makes_no_keychain_call()
    {
        _rig.WriteState($$"""{"project":"{{Label}}","keyPointer":"file:/home/x/.sindri/secrets/onyx-sindri-onyx"}""");

        var exit = await _rig.Run(human: true, answer: "yes");

        Assert.Equal(0, exit);
        Assert.DoesNotContain(_rig.Calls, c => c.StartsWith("keychain", StringComparison.Ordinal));
        Assert.False(Directory.Exists(_rig.OnyxDirectory));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_state_without_a_project_falls_back_to_the_name_of_this_home()
    {
        _rig.WriteState("""{"port":3000}""");
        var name = Features.Leftovers.OnyxLeftovers.ProjectNameFor(_rig.Home);
        _rig.Docker.Containers["mine"] = name;
        _rig.Docker.Containers["other"] = Label;

        var exit = await _rig.Run(human: true, answer: "yes");

        Assert.Equal(0, exit);
        Assert.Equal(["other"], _rig.Docker.Containers.Keys, StringComparer.Ordinal);
    }
}
