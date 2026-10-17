using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Refs;

namespace Aw.Forge.Testing;

/// <summary>An adapter ready for a contract run.</summary>
/// <param name="Forge">The adapter.</param>
/// <param name="Seed">Its seeding side, or null for an adapter that cannot play the other side (GitHub).</param>
/// <param name="Repo">A repository on an allowed host that the run may use.</param>
/// <param name="Requests">Reads how many calls reached the store or the network, or null when the adapter can't count.</param>
/// <param name="CanPush">Whether Push works in this run without a real repository (the fake simulates it).</param>
public sealed record ForgeUnderTest(IForge Forge, IForgeSeed? Seed, ForgeRepo Repo, Func<int>? Requests, bool CanPush);
