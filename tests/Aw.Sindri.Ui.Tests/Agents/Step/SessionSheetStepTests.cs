using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Step;

/// <summary>The session sheet shows the Step an engine launch started the session for, and opens its frame (L2). The session is sessions-id/cursor-engine-launch.json.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionSheetStepTests
{
    private const string Instance = "ITM-00000000000000000000000000000001/step/1";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SessionSheetViewModel> Opened(string reference)
    {
        var sheet = new SessionSheetViewModel(_server);
        await sheet.Open(_api, reference, Token);
        return sheet;
    }

    [Fact]
    public async Task An_engine_launch_lists_the_instance_the_state_and_the_attempt_as_fields()
    {
        var sheet = await Opened("cursor/pid-333-100");

        Assert.Equal((Instance, "build", "2"), (Field(sheet, "Run"), Field(sheet, "Step"), Field(sheet, "Attempt")));
    }

    [Fact]
    public async Task A_session_without_a_Step_lists_none_of_them()
    {
        var sheet = await Opened("claude-code/aaaaaaaa-0000-4000-8000-000000000001");

        Assert.DoesNotContain(sheet.Fields, f => new[] { "Run", "Step", "Attempt" }.Contains(f.Label, StringComparer.Ordinal));
    }

    private static string Field(SessionSheetViewModel sheet, string label) => sheet.Fields.Single(f => f.Label == label).Value;

}
