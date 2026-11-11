using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.CreateToolkit;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.BuildToolkit;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>CreateToolkit (agent-first PR 16c): the generator makes the folder, a row in state 'off' records it with its creator, and a bad or taken name never reaches the generator.</summary>
[Lane(TestLane.Unit)]
public sealed class CreateToolkitTests : IDisposable
{
    private readonly ToolkitRig _rig = new();

    private static CancellationToken Token => ToolkitRig.Token;

    public void Dispose() => _rig.Dispose();

    private CreateToolkitService Create(Core.Commands.CallerContext? caller = null) => new(_rig.Store, _rig.Folders, _rig.Jobs, new FixedCallerContextAccessor(caller));

    [Fact]
    public async Task A_new_toolkit_is_generated_and_recorded_off_with_the_callers_actor()
    {
        var caller = A.Caller().Session().Build();

        var made = await Create(caller).Handle(new CreateToolkitRequest("github-triage", "list_issues", "database:state,connection:github"), Token);

        Assert.True(made.IsSuccess);
        Assert.Equal(("github-triage", "/tk/github-triage", ToolkitState.Off), (made.Value.Name, made.Value.Dir, made.Value.State));
        Assert.Equal([("github-triage", "list_issues", (string?)"database:state,connection:github")], _rig.Folders.Generated);
        var row = _rig.Store.Items["github-triage"];
        Assert.Equal((ToolkitState.Off, "/tk/github-triage", caller.Actor), (row.State, row.Dir, row.CreatedBy));
        Assert.Null(row.SourceHash);
        Assert.Null(row.Manifest);
    }

    [Fact]
    public async Task A_call_with_no_caller_context_is_recorded_as_the_owner()
    {
        _ = await Create().Handle(new CreateToolkitRequest("notes", "add_note"), Token);

        Assert.Equal("owner", _rig.Store.Items["notes"].CreatedBy);
    }

    [Theory]
    [InlineData("Notes")]
    [InlineData("two_words")]
    [InlineData("2fa")]
    [InlineData("a--b")]
    [InlineData("a-2b")]
    [InlineData("trailing-")]
    [InlineData("")]
    [InlineData("x-ray")]       // "XRay" comes back from the generator's kebab as "xray"
    [InlineData("my-a-tool")]   // "MyATool" -> "my-atool"
    [InlineData("a-b")]         // "AB" is under the generator's 3 characters
    [InlineData("common")]      // a generator reserved name
    [InlineData("a-very-long-toolkit-name-that-goes-on")]   // over the generator's 31 characters once the '-' are dropped
    public async Task A_name_the_generator_could_not_turn_back_into_the_same_folder_is_refused_before_it_runs(string name)
    {
        var made = await Create().Handle(new CreateToolkitRequest(name, "add_note"), Token);

        Assert.Equal(SindriErrors.ToolkitNameInvalid.Code, made.Error.Code);
        Assert.Empty(_rig.Folders.Generated);
        Assert.Equal(["triage"], _rig.Store.Items.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_name_that_has_a_row_is_refused_and_the_row_stays_as_it_was()
    {
        var before = _rig.Store.Items["triage"];

        var made = await Create().Handle(new CreateToolkitRequest("triage", "ping"), Token);

        Assert.Equal(SindriErrors.ToolkitExists.Code, made.Error.Code);
        Assert.Empty(_rig.Folders.Generated);
        Assert.Same(before, _rig.Store.Items["triage"]);
    }

    [Fact]
    public async Task A_generator_failure_is_returned_and_writes_no_row()
    {
        _rig.Folders.Failure = SindriErrors.ToolkitCreateFailed.With("NT-SDK: no Aw.Sindri.Toolkit package in the SDK feed");

        var made = await Create().Handle(new CreateToolkitRequest("notes", "add_note"), Token);

        Assert.Equal(SindriErrors.ToolkitCreateFailed.Code, made.Error.Code);
        Assert.Contains("NT-SDK", made.Error.Message, StringComparison.Ordinal);
        Assert.False(_rig.Store.Items.ContainsKey("notes"));
    }

    [Fact]
    public async Task A_created_toolkit_goes_through_build_and_start_with_no_hand_made_row()
    {
        _ = await Create().Handle(new CreateToolkitRequest("notes", "add_note"), Token);
        var note = _rig.Store.Items["notes"];

        var built = await new Features.BuildToolkit.BuildToolkitService(_rig.Store, _rig.Workspace, _rig.Commands, _rig.Slots, _rig.Jobs, TimeProvider.System).Handle(new Features.BuildToolkit.BuildToolkitRequest("notes"), Token);
        var started = await new Features.StartToolkit.StartToolkitService(_rig.Store, _rig.Workspace, _rig.Runtime).Handle(new Features.StartToolkit.StartToolkitRequest("notes"), Token);

        Assert.Equal(ToolkitState.Off, note.State);
        Assert.True(built.IsSuccess);
        Assert.True(started.IsSuccess);
        Assert.Equal(ToolkitState.Running, started.Value.State);
    }
}
