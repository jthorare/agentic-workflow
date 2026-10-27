using Aw.BugFix.Core.Model;
using Aw.BugFix.Host;
using Aw.BugFix.Infrastructure;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cutover;

/// <summary>
/// The helper's side of the resolution-check digest (#1290): the shared real input (tests/fixtures/judge-input/resolution-check.json,
/// every field the helper sends) read into the helper's record and written back in the judge's canonical form is the same bytes.
/// Aw.Judge.Tests' ResolutionCheckSharedInputTests digests the same file, so a field one side drops fails one of the two.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JudgeSharedInputTests
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(BugFixJsonContext.Default);
    private static readonly byte[] _shared = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "JudgeInput", "resolution-check.json"));

    [Fact]
    public void The_shared_input_carries_every_field_the_helper_sends_and_writes_back_byte_identical()
    {
        var input = AwJson.Deserialize<ResolutionCheckInput>(_shared, _json).Value;

        Assert.All([input.Diff, input.CheckSource, input.BaselineFailure], Assert.NotNull);
        Assert.Equal(_shared, JudgeInputForm.Canonical(input, _json));
    }
}
