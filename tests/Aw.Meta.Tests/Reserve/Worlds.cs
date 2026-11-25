using Aw.Meta.Core.Coordination;
using Aw.Meta.Features.Status;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Reserve;

/// <summary>The queue, reservation and advice services over a world's fakes.</summary>
internal static class Worlds
{
    internal static MergeQueueReader Queue(MetaWorld world) => new(world.Messages, world.Gates, world.Values, world.Repo, world.Config);

    internal static StatusReader StatusReader(MetaWorld world) => new(world.Reader, Queue(world), world.Owner, world.Lanes, world.Context);

    internal static HeavyReservation Reservation(MetaWorld world) => new(world.Heavy, Queue(world), world.Log, world.Config, world.Clock);

    internal static GithubQueueWatch QueueWatch(MetaWorld world) => new(world.Repo, world.Values, world.Gates, world.Log, world.Clock);

    internal static PollFollowUps FollowUps(MetaWorld world) => new(new WatchSweeper(world.Config, world.Sweeper, world.Values), QueueWatch(world));

    internal static ResourceAdvisor Advisor(MetaWorld world) => new(world.Heavy, Reservation(world), world.Clock);
}
