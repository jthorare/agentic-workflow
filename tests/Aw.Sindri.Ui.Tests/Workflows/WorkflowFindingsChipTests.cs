using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The findings chip (spec 4.5, 4.7, 4.8): "N things to fix" from POST /sindri/workflows/validate, and the header's one primary button.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowFindingsChipTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Two = """
        {"passed":false,"hash":null,"findings":[
          {"code":"GRAPH-REF-001","severity":"error","nodes":["agent"],"field":"prompt","message":"prompt reads a missing output","fix":"Pick an output","id":"f1","node":"agent","cause":"prompt reads a missing output"},
          {"code":"GRAPH-CFG-002","severity":"error","nodes":["agent"],"field":"repo","message":"repo is empty","fix":"Pick a repo","id":"f2","node":"agent","cause":"repo is empty"},
          {"code":"GRAPH-NAME-002","severity":"warning","nodes":["start"],"field":null,"message":"Couldn't check","fix":"","id":"f3","node":"start","cause":"Couldn't check"}
        ]}
        """;

#pragma warning restore MA0136

    private const string Clean = """{"passed":true,"hash":"abc","findings":[]}""";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ViewModels.Workflows.WorkflowsPageViewModel> Open(string validated, HttpStatusCode status = HttpStatusCode.OK)
    {
        _f.Server.ValidateWorkflowAnswer = new RouteAnswer(status, validated);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.Validating;
        return page;
    }

    [Fact]
    public async Task Two_errors_and_a_warning_say_2_things_to_fix_and_the_check_ran_on_the_newest_revision()
    {
        var page = await Open(Two);

        Assert.Equal("2 things to fix", page.FindingsText);
        Assert.True(page.HasFindings);
        Assert.Equal(3, page.Findings.Count);
        Assert.Equal<(string, int?)>([(Slug, 2)], _f.Server.ValidatedWorkflows);
    }

    [Fact]
    public async Task One_error_says_1_thing_to_fix_and_the_primary_button_says_Fix_1_thing()
    {
        var one = Two.Replace("""{"code":"GRAPH-CFG-002","severity":"error","nodes":["agent"],"field":"repo","message":"repo is empty","fix":"Pick a repo","id":"f2","node":"agent","cause":"repo is empty"},""", string.Empty, StringComparison.Ordinal);

        var page = await Open(one);

        Assert.Equal("1 thing to fix", page.FindingsText);
        Assert.Equal("Fix 1 thing", page.PrimaryText);
    }

    [Fact]
    public async Task A_warning_alone_is_no_chip_and_the_next_step_is_the_test_run()
    {
        var page = await Open("""{"passed":true,"hash":"abc","findings":[{"code":"GRAPH-NAME-002","severity":"warning","nodes":[],"field":null,"message":"Couldn't check","fix":"","id":"w1","node":null,"cause":"Couldn't check"}]}""");

        Assert.Equal(string.Empty, page.FindingsText);
        Assert.False(page.HasFindings);
        Assert.Equal("Test run", page.PrimaryText);
    }

    [Fact]
    public async Task A_clean_check_shows_no_chip()
    {
        var page = await Open(Clean);

        Assert.False(page.HasFindings);
        Assert.False(page.ValidationNotBuilt);
        Assert.Empty(page.Findings);
    }

    [Fact]
    public async Task A_check_that_is_not_built_yet_is_a_quiet_state_not_an_error()
    {
        var page = await _f.Shown(Token);   // the fake answers SND-GRAPH-113 until the engine builds the route
        await page.OpenWorkflow(Slug, Token);
        await page.Validating;

        Assert.True(page.ValidationNotBuilt);
        Assert.False(page.HasFindings);
        Assert.Equal(string.Empty, page.FindingsText);
        Assert.False(page.Runs.IsFailed);
    }
}
