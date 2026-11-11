using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Defects;
using Aw.Meta.Features.ReportDefect;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.ReportDefect;

public sealed class ReportDefectTests
{
    private const string Repo = "jthorare/agentic-workflow";
    private const string Created = "https://github.com/jthorare/agentic-workflow/issues/77";

    private readonly MetaWorld _world = new();

    private ReportDefectService Service() => new(_world.Config, _world.Repo, _world.Values, _world.Log, _world.Owner, _world.Clock);

    private static ReportDefectRequest Defect(string title = "recycle fails", string? path = null) =>
        new("AW-META-010", title, "lane-recycle exited 1 on a busy lane", "AW-META-010: recycle failed", path);

    private async Task<string?> Day() => (await _world.Values.Get(KvKeys.DefectsDay, CancellationToken.None)).Value;

    private IEnumerable<string> GhLines(string verb) => _world.Runner.Lines.Where(l => l.Contains("issue " + verb, StringComparison.Ordinal));

    private static string Issue(int number, string title, string state, string? closedAt = null) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"number":{{number}},"title":"{{title}}","state":"{{state}}","closedAt":{{(closedAt is null ? "null" : "\"" + closedAt + "\"")}},"url":"https://github.com/jthorare/agentic-workflow/issues/{{number}}"}""");

    private void Search(params string[] issues) => _world.Runner.When("issue list", "[" + string.Join(',', issues) + "]").When("issue create", Created + "\n").When("issue comment", "ok");

    private async Task<ReportDefectResponse> Report(ReportDefectRequest request)
    {
        var result = await Service().Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task<string?> StatusDefects()
    {
        var service = new StatusService(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock);
        return (await service.Handle(new StatusRequest(), CancellationToken.None)).Value.Defects;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_defect_is_filed_logged_and_told_to_the_owner()
    {
        await _world.Ready();
        Search();

        var response = await Report(Defect());

        var create = Assert.Single(GhLines("create"));
        Assert.Contains("-R " + Repo, create, StringComparison.Ordinal);
        Assert.Contains("--title AW-META-010: recycle fails", create, StringComparison.Ordinal);
        Assert.All(_world.Runner.Requests, r => Assert.Equal("/work", r.Cwd));
        Assert.Equal(("filed", Created), (response.Outcome, response.Url));
        var line = $"defect filed {Created}: AW-META-010: recycle fails";
        Assert.Equal([$"- NOTE 2026-10-15 05:00: {line}"], _world.Log.Lines);
        Assert.Equal(response.LogLine, Assert.Single(_world.Log.Lines));
        var question = Assert.Single((await _world.Owner.List(includeAnswered: false, CancellationToken.None)).Value);
        Assert.Equal(("meta", $"FYI, no answer needed: filed {Created}: AW-META-010: recycle fails"), (question.AskedBy, question.Text));
        Assert.Equal("2026-10-15|1|0", await Day());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_issue_with_the_same_title_gets_a_comment_and_no_FYI()
    {
        await _world.Ready();
        Search(Issue(40, "AW-META-010: recycle fails", "OPEN"));

        var response = await Report(Defect());

        Assert.Empty(GhLines("create"));
        Assert.Contains("issue comment 40 -R " + Repo, Assert.Single(GhLines("comment")), StringComparison.Ordinal);
        Assert.Equal(("commented", "https://github.com/jthorare/agentic-workflow/issues/40"), (response.Outcome, response.Url));
        Assert.Contains("defect seen again, commented on https://github.com/jthorare/agentic-workflow/issues/40: AW-META-010: recycle fails", Assert.Single(_world.Log.Lines), StringComparison.Ordinal);
        Assert.Empty((await _world.Owner.List(includeAnswered: true, CancellationToken.None)).Value);
        Assert.Null(await Day());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_closed_issue_counts_for_fourteen_days_and_not_longer()
    {
        await _world.Ready();   // the clock reads 2026-10-15 05:00 UTC
        Search(Issue(41, "AW-META-010: recycle fails", "CLOSED", "2026-10-12T04:00:00Z"));

        var recent = await Report(Defect());

        Assert.Equal("commented", recent.Outcome);

        var later = new MetaWorld();
        await later.Ready();
        _ = later.Runner.When("issue list", "[" + Issue(41, "AW-META-010: recycle fails", "CLOSED", "2026-09-25T04:00:00Z") + "]").When("issue create", Created + "\n");
        var service = new ReportDefectService(later.Config, later.Repo, later.Values, later.Log, later.Owner, later.Clock);

        var old = await service.Handle(Defect(), CancellationToken.None);

        Assert.Equal("filed", old.Value.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_same_code_issue_with_another_title_does_not_match()
    {
        await _world.Ready();
        Search(Issue(42, "AW-META-010: something else", "OPEN"));

        var response = await Report(Defect());

        Assert.Equal("filed", response.Outcome);
        Assert.Empty(GhLines("comment"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_fourth_new_defect_of_a_day_files_nothing_and_the_status_says_so()
    {
        await _world.Ready();
        Search();
        foreach (var n in new[] { "a", "b", "c" })
        {
            _ = await Report(Defect("defect " + n));
        }

        var fourth = await Report(Defect("defect d"));

        Assert.Equal(3, GhLines("create").Count());
        Assert.Equal(("cap-hit", null), (fourth.Outcome, fourth.Url));
        Assert.EndsWith("issue cap hit (1 skipped today): AW-META-010: defect d", fourth.LogLine, StringComparison.Ordinal);
        Assert.Equal("2026-10-15|3|1", await Day());
        Assert.Equal("issue cap hit (1 skipped)", await StatusDefects());
        Assert.Equal(3, (await _world.Owner.List(includeAnswered: true, CancellationToken.None)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_cap_resets_on_the_next_UTC_day()
    {
        await _world.Ready();
        Search();
        for (var i = 0; i < 4; i++)
        {
            _ = await Report(Defect("defect " + i.ToString(CultureInfo.InvariantCulture)));
        }

        _world.Clock.Advance(TimeSpan.FromHours(24));
        var next = await Report(Defect("defect next"));

        Assert.Equal("filed", next.Outcome);
        Assert.Equal("2026-10-16|1|0", await Day());
        Assert.Null(await StatusDefects());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_issue_create_is_AW_META_012_and_changes_nothing()
    {
        await _world.Ready();
        _ = _world.Runner.When("issue list", "[]").When("issue create", "", exitCode: 1);

        var result = await Service().Handle(Defect(), CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Empty(_world.Log.Lines);
        Assert.Null(await Day());
        Assert.Empty((await _world.Owner.List(includeAnswered: true, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_validator_refuses_blank_fields_a_bad_code_a_two_line_error_and_a_long_title()
    {
        var validator = new ReportDefectValidator();
        var ok = Defect();

        Assert.True(validator.Validate(ok).IsSuccess);
        Assert.True(validator.Validate(ok with { Code = " " }).IsError);
        Assert.True(validator.Validate(ok with { Title = "" }).IsError);
        Assert.True(validator.Validate(ok with { What = "  " }).IsError);
        Assert.True(validator.Validate(ok with { ErrorLine = "" }).IsError);
        Assert.True(validator.Validate(ok with { Code = "aw-meta-010" }).IsError);
        Assert.True(validator.Validate(ok with { Code = "AW" }).IsError);
        Assert.True(validator.Validate(ok with { ErrorLine = "one\ntwo" }).IsError);
        Assert.True(validator.Validate(ok with { Title = new string('x', 121) }).IsError);
        Assert.True(validator.Validate(ok with { Title = "a\nb" }).IsError);
        Assert.True(validator.Validate(ok with { ErrorLine = new string('x', 401) }).IsError);
        Assert.True(validator.Validate(ok with { What = new string('x', 1501) }).IsError);
        Assert.True(validator.Validate(ok with { Path = new string('x', 201) }).IsError);
        Assert.True(validator.Validate(ok with { Path = "a\nb" }).IsError);
        Assert.True(validator.Validate(ok with { Code = "SND-SCRUB-002", Path = "src/x.cs" }).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_body_names_the_path_only_when_one_is_given()
    {
        var without = DefectText.Body("it broke", "AW-META-010: x", path: null);
        var with = DefectText.Body("it broke", "AW-META-010: x", "src/Meta/X.cs");

        Assert.DoesNotContain("Where:", without, StringComparison.Ordinal);
        Assert.Contains("Where: `src/Meta/X.cs`", with, StringComparison.Ordinal);
        Assert.Contains("## What happens\n\nit broke\n\nError line:\n\n```\nAW-META-010: x\n```\n", with, StringComparison.Ordinal);
        Assert.EndsWith("Filed by `aw-meta report-defect`.", with.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal("AW-META-010: recycle fails", DefectText.Title("AW-META-010", "recycle fails"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_day_counts_round_trip_and_another_day_reads_as_zero()
    {
        var today = new DateOnly(2026, 10, 15);

        Assert.Equal("2026-10-15|2|1", DefectDay.Format(today, 2, 1));
        Assert.Equal((2, 1), DefectDay.Parse("2026-10-15|2|1", today));
        Assert.Equal((0, 0), DefectDay.Parse("2026-10-14|2|1", today));
        Assert.Equal((0, 0), DefectDay.Parse("junk", today));
        Assert.Equal((0, 0), DefectDay.Parse(null, today));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_team_file_without_defects_reads_the_defaults()
    {
        var team = new TeamConfig("t1", "/work", ["meta"], "meta");

        Assert.Equal(("jthorare/agentic-workflow", 3), (team.Defects.Repo, team.Defects.DailyCap));
    }
}
