namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Tests that assert on wall clock (the Phase 5 signal's "in under a second"), or that wait on a time-skipping server's
/// RPC deadline (the parking tests' DelayAsync, #2574): they run alone within this assembly, after its parallel tests,
/// so its other test servers and workers don't eat their time. Other test assemblies in a solution run still share the
/// machine, so a bound here has to leave room for that.
/// </summary>
[CollectionDefinition(nameof(WallClockAlone), DisableParallelization = true)]
public sealed class WallClockAlone;
