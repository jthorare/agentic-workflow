using Aw.Sindri.Infrastructure.Locks;

namespace Aw.Sindri.Tests.Locks;

/// <summary>The heavy lock's slot count follows the box's cap rule (#1124): the cap file $AW_STATE_DIR/heavy-slots-cap, then AW_HEAVY_SLOTS, then 1.</summary>
public sealed class HeavyLockSettingsCapTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-heavy-cap-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static Func<string, string?> Env(string? slots) => name => name == "AW_HEAVY_SLOTS" ? slots : null;

    private void WriteCap(string text) => File.WriteAllText(Path.Combine(_root, "heavy-slots-cap"), text);

    [Fact, Trait("Lane", "unit")]
    public void The_cap_file_sets_the_slots_when_AW_HEAVY_SLOTS_is_unset()
    {
        WriteCap("4\n");

        Assert.Equal(4, HeavyLockSettings.Read(Env(slots: null), _root).Slots);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_cap_file_wins_over_AW_HEAVY_SLOTS()
    {
        WriteCap("4\n");

        Assert.Equal(4, HeavyLockSettings.Read(Env("2"), _root).Slots);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_cap_file_AW_HEAVY_SLOTS_sets_the_slots() =>
        Assert.Equal(3, HeavyLockSettings.Read(Env("3"), _root).Slots);

    [Fact, Trait("Lane", "unit")]
    public void With_neither_the_slots_are_one() =>
        Assert.Equal(1, HeavyLockSettings.Read(Env(slots: null), _root).Slots);
}
