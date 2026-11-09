using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.BoardLine;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.BoardLine;

public sealed class BoardLineTests
{
    private const string Sha12 = "abc1234def5678900000000000000000000000aa";
    private const string Sha13 = "fed9876cba5432100000000000000000000000bb";

    private readonly MetaWorld _world = new();

    private BoardLineService Service() => new(_world.Config, _world.Board, _world.Lanes);

    private async Task<string?> Line(LaneIdentity lane)
    {
        var result = await Service().Handle(new BoardLineRequest(lane), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.Line;
    }

    private async Task Merge(int pr, string sha, int minute) =>
        _ = await _world.Board.RecordMain(sha, [pr], A.At(minute), CancellationToken.None);

    private async Task LaneMap(int version) =>
        _ = await _world.Lanes.Save(new LaneMapState(version, [A.Assigned("api", "t1-api")]), A.At(version), CancellationToken.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_call_shows_the_board_and_a_second_call_shows_nothing()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        await Merge(12, Sha12, 5);
        await LaneMap(5);

        var first = await Line(lane);
        var second = await Line(lane);

        Assert.NotNull(first);
        Assert.Contains("abc1234", first, StringComparison.Ordinal);
        Assert.Contains("#12", first, StringComparison.Ordinal);
        Assert.Contains("lane map v5", first, StringComparison.Ordinal);
        Assert.Null(second);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_merge_shows_its_short_sha_and_pull_request()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        await Merge(12, Sha12, 5);
        await LaneMap(5);
        _ = await Line(lane);
        await Merge(13, Sha13, 8);

        var line = await Line(lane);

        Assert.NotNull(line);
        Assert.Contains("fed9876", line, StringComparison.Ordinal);
        Assert.Contains("#13", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_lane_map_version_without_a_merge_names_the_version()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        await Merge(12, Sha12, 5);
        await LaneMap(5);
        _ = await Line(lane);
        await LaneMap(6);

        var line = await Line(lane);

        Assert.NotNull(line);
        Assert.Contains("lane map v6", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lanes_see_the_board_independently()
    {
        await _world.Ready();
        var api = _world.PutLane("api");
        var ui = _world.PutLane("ui");
        await Merge(12, Sha12, 5);
        await LaneMap(5);

        _ = await Line(api);
        var uiFirst = await Line(ui);

        Assert.NotNull(uiFirst);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_team_config_it_answers_null_and_records_nothing()
    {
        await Merge(12, Sha12, 5);
        await LaneMap(5);
        var lane = new LaneIdentity("t1", "api");

        var result = await Service().Handle(new BoardLineRequest(lane), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Line);
        Assert.Null((await _world.Board.SeenVersion("api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_line_is_one_line_of_at_most_200_characters()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        await Merge(12, Sha12, 5);
        await LaneMap(5);

        var line = await Line(lane);

        Assert.NotNull(line);
        Assert.DoesNotContain('\n', line);
        Assert.InRange(line.Length, 1, 200);
    }
}
