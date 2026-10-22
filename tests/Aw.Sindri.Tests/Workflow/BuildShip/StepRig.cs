using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>
/// The fakes a build or ship Step runs over: the profile's commands (command lines), command runs by command line, one worktree, a phase packet (base 'b0', allowed 'src/**' and 'new: docs/x.md',
/// 'testChange' for tests/old.test.ts), the branch's changes and a heavy lock that counts its takes and releases.
/// </summary>
internal sealed class StepRig
{
    internal const string Tree = "/work/p1";
    internal const string PacketDir = "/packets/p1";

    internal StepRig(params string[] profileCommands)
        : this(TimeSpan.FromHours(2), profileCommands)
    {
    }

    internal StepRig(TimeSpan each, params string[] profileCommands)
    {
        Clock = new FakeTimeProvider(KnownInstants.Epoch);
        Slots = new CountingSlots();
        Commands = new InMemoryCheckCommands();
        Changes = new InMemoryBranchChanges();
        Diff = new FakeRunDiff();
        Diff.Add(Tree, "h1");
        Files = new InMemoryPacketDirectory().Put(PacketDir, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["packet.json"] = """{"id":"p1","baseSha":"b0"}""",
            ["phase.json"] = """{"allowedFiles":["src/**","new: docs/x.md"],"testChange":[{"path":"tests/old.test.ts","reason":"renamed"}]}""",
        });
        Runner = new RepoCommandRunner(new StubCommandSource(profileCommands, each), Commands, Slots, Clock);
    }

    internal FakeTimeProvider Clock { get; }

    internal CountingSlots Slots { get; }

    internal InMemoryCheckCommands Commands { get; }

    internal InMemoryBranchChanges Changes { get; }

    internal FakeRunDiff Diff { get; }

    internal InMemoryPacketDirectory Files { get; }

    internal RepoCommandRunner Runner { get; }

    internal static CheckRun Pass(string tail = "ok") => CheckRun.Of(0, timedOut: false, TimeSpan.FromSeconds(2), tail);

    internal static CheckRun Fail(string tail = "1 failed") => CheckRun.Of(1, timedOut: false, TimeSpan.FromSeconds(2), tail);

    /// <summary>The branch from b0 to h1 changed these paths and added these lines, in these commits.</summary>
    internal StepRig Branch(ChangedFile[] files, AddedLine[]? lines = null, ChangedCommit[]? commits = null)
    {
        _ = Changes.Add(Tree, "b0", "h1", new BranchChanges(files, lines ?? [], commits ?? [new ChangedCommit("h1aaaaaaaa", "feat: add", IsMerge: false)]));
        return this;
    }

    internal static StepContext Context(string step, Dictionary<string, string> facts) => BuildShipContexts.Of(step, facts);

    internal static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    /// <summary>A heavy lock with one slot that counts takes and releases.</summary>
    internal sealed class CountingSlots : IHeavySlots
    {
        internal int Taken { get; private set; }

        internal int Released { get; private set; }

        internal bool Busy { get; set; }

        /// <summary>Gets the wait each take was given.</summary>
        internal List<TimeSpan> Waits { get; } = [];

        /// <summary>Gets or sets what happens while a take waits (a test moves its clock here).</summary>
        internal Action? OnTake { get; set; }

        public ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken)
        {
            Waits.Add(timeout);
            OnTake?.Invoke();
            if (Busy)
            {
                return ValueTask.FromResult<Result<HeavySlotTaken>>(new HeavySlotTaken(Lease: null, Busy: "slot 1/1 held by someone"));
            }

            Taken++;
            return ValueTask.FromResult<Result<HeavySlotTaken>>(new HeavySlotTaken(new Lease(this), string.Empty));
        }

        private sealed class Lease(CountingSlots owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.Released++;
                return ValueTask.CompletedTask;
            }
        }
    }
}
