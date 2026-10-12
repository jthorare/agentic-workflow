using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Scheduling;

public sealed class ServiceScheduleTests
{
    private static readonly DateTimeOffset _t0 = KnownInstants.Epoch;
    private static readonly ServiceSchedule _every5s = new(ServiceName.From("heartbeat"), new Cadence(TimeSpan.FromSeconds(5)), _t0);

    [Fact, Trait("Lane", "unit")]
    public void On_time_the_next_slot_is_one_cadence_later() =>
        Assert.Equal(_t0.AddSeconds(5), _every5s.Advance(_t0.AddMilliseconds(10)).NextSlot);

    [Fact, Trait("Lane", "unit")]
    public void After_a_sleep_missed_slots_are_skipped_not_replayed() =>
        Assert.Equal(_t0.AddSeconds(65), _every5s.Advance(_t0.AddSeconds(62)).NextSlot);

    [Fact, Trait("Lane", "unit")]
    public void The_profile_names_the_services_and_their_cadence()
    {
        var profile = new SindriProfile(1, "thor",
            Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new("2m") });

        var only = Assert.Single(ServiceSchedule.From(profile, _t0));

        Assert.Equal(new ServiceSchedule(ServiceName.From("heartbeat"), new Cadence(TimeSpan.FromMinutes(2)), _t0), only);
    }
}
