using System.Text.Json.Nodes;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The state and accessibility goldens the UI reads (sandbox spec §8; SB-0).</summary>
public sealed class SandboxStatesTests
{
    private static readonly string[] _surfaces =
        ["checkAgain", "parkCard", "sandboxLine", "blockedList", "allow", "waiting", "limitEnding", "healthRow", "settingsSection"];

    private static readonly string[] _columns = ["control", "keyboard", "screenReader", "focus", "textScaleAndColour"];
    private static readonly string[] _routes = ["expensive", "light", "direct"];

    [Fact, Trait("Lane", "unit")]
    public void Every_surface_has_its_states()
    {
        var states = SandboxGoldens.Object("states.json");

        foreach (var surface in _surfaces)
        {
            var ids = states[surface]!.AsArray().Select(s => (string)s!["id"]!).ToList();

            Assert.NotEmpty(ids);
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_named_states_exist()
    {
        var states = SandboxGoldens.Object("states.json");

        Assert.Equal(["idle", "in-flight", "still-failing", "all-clear", "resumed", "resumed-partial"], Ids(states, "checkAgain"));
        Assert.Equal(["idle", "sending", "needs-approval", "allowed", "refused"], Ids(states, "allow"));
        Assert.Equal(["sandboxed-step", "sandboxed-attended", "plan-unreadable", "serve-down"], Ids(states, "sandboxLine"));
        Assert.Equal(["ready", "blocked", "learning", "serve-down", "never-checked"], Ids(states, "healthRow"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Check_again_announces_sending_after_three_hundred_milliseconds()
    {
        var inFlight = SandboxGoldens.Object("states.json")["checkAgain"]!.AsArray().Single(s => (string)s!["id"]! == "in-flight")!;

        Assert.Equal(("Sending…", 300), ((string)inFlight["announce"]!, (int)inFlight["announceAfterMs"]!));
    }

    [Fact, Trait("Lane", "unit")]
    public void Ten_accessibility_rows_each_with_all_five_columns()
    {
        var rows = SandboxGoldens.Object("accessibility.json")["rows"]!.AsArray();

        Assert.Equal(10, rows.Count);
        Assert.All(rows, r => Assert.All(_columns, k => Assert.False(string.IsNullOrWhiteSpace((string?)r![k]), k)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shim_table_routes_are_closed_and_cover_the_three_shims()
    {
        var shims = SandboxGoldens.Object("shims.json")["shims"]!.AsArray();

        Assert.Equal(["dotnet", "npm", "npx"], shims.Select(s => (string)s!["shim"]!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(shims, s => Assert.Contains((string)s!["route"]!, _routes, StringComparer.Ordinal));
        Assert.Equal("AW_HEAVY_LOCK_HELD", (string)SandboxGoldens.Object("shims.json")["heldSlot"]!["neverInEnvironment"]!, StringComparer.Ordinal);
    }

    private static List<string> Ids(JsonObject states, string surface) => [.. states[surface]!.AsArray().Select(s => (string)s!["id"]!)];
}
