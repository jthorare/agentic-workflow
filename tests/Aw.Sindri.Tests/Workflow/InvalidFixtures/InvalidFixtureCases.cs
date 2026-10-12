namespace Aw.Sindri.Tests.Workflow.InvalidFixtures;

/// <summary>
/// Each invalid fixture's end-to-end findings: its own rule's line (the one its rule-level test asserts, at the same
/// position) and every other line it gets. Most fixtures have one fault and nothing else; the capability fixtures name
/// effects, which RCP-EFFECT-001 skips without a profile key.
/// </summary>
internal static class InvalidFixtureCases
{
    public static IReadOnlyDictionary<string, InvalidFixtureCase> All { get; } = new Dictionary<string, InvalidFixtureCase>(StringComparer.Ordinal)
    {
        ["RCP-BLOCK-001"] = new(
            FixtureSetup.Shipped,
            "RCP-BLOCK-001: block 'step.prodcue' isn't registered at RCP-BLOCK-001.yaml:8:5 (states.produce.step.use); name a registered step block, or fix the id",
            []),
        ["RCP-BLOCK-002"] = new(
            FixtureSetup.Shipped,
            "RCP-BLOCK-002: 'step.verify' is a step block, but a transition's gates take gate blocks at RCP-BLOCK-002.yaml:15:85 (transitions[0].gates[0].use); name a gate block here (gate.<area>.<name>)",
            []),
        ["RCP-BLOCK-003"] = new(
            FixtureSetup.Shipped,
            "RCP-BLOCK-003: block 'gate.budget.available' has no parameter 'limit' at RCP-BLOCK-003.yaml:16:53 (transitions[0].gates[0].with.limit); remove it, since 'gate.budget.available' takes no parameters",
            []),
        ["RCP-CAP-001"] = new(
            FixtureSetup.CapabilityWithBase,
            "RCP-CAP-001: capability use effect.claim.ref on publish -> running isn't in base workflow.cap-base at RCP-CAP-001.yaml:49:9 (transitions[2].effects[1].use); remove it, or generate the recipe from a base that has it",
            [
                "RCP-EFFECT-001: effect 'effect.repo.push' wasn't checked against an effect allowlist, since the profile has no workflow.effects at RCP-CAP-001.yaml:47:9 (transitions[2].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time [Skipped]",
                "RCP-EFFECT-001: effect 'effect.claim.ref' wasn't checked against an effect allowlist, since the profile has no workflow.effects at RCP-CAP-001.yaml:49:9 (transitions[2].effects[1].use); add workflow.effects to the profile; until then the effect is refused at run time [Skipped]",
            ]),
        ["RCP-CAP-002"] = new(
            FixtureSetup.CapabilityWithBase,
            "RCP-CAP-002: capability parameter 'remote' of effect.repo.push on publish -> running is \"upstream\", but base workflow.cap-base has \"origin\" at RCP-CAP-002.yaml:48:17 (transitions[2].effects[0].with.remote); set it to the base's value",
            [
                "RCP-EFFECT-001: effect 'effect.repo.push' wasn't checked against an effect allowlist, since the profile has no workflow.effects at RCP-CAP-002.yaml:47:9 (transitions[2].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time [Skipped]",
            ]),
        ["RCP-CAP-004"] = new(
            FixtureSetup.Capability,
            "RCP-CAP-004: hand-written recipe workflow.cap-handwritten names base workflow.cap-base@5aa84cb58a5a at RCP-CAP-004.yaml:4:1 (base); remove base, which only Sindri's generator writes",
            []),
        ["RCP-CAP-005"] = new(
            FixtureSetup.CapabilityWithBase,
            "RCP-CAP-005: capability use effect.repo.push on publish -> running is guarded by gate.review at threshold 0.6, below base workflow.cap-base's 0.8 at RCP-CAP-005.yaml:47:9 (transitions[2].effects[0].use); raise the threshold to 0.8 or more",
            [
                "RCP-EFFECT-001: effect 'effect.repo.push' wasn't checked against an effect allowlist, since the profile has no workflow.effects at RCP-CAP-005.yaml:47:9 (transitions[2].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time [Skipped]",
            ]),
        ["RCP-CAP-006"] = new(
            FixtureSetup.CapabilityWithBase,
            "RCP-CAP-006: loop 'revisions' on state 'produce' has cap 5, above base workflow.cap-base's 3 at RCP-CAP-006.yaml:52:23 (loops.revisions.cap); lower cap to 3 or less",
            [
                "RCP-EFFECT-001: effect 'effect.repo.push' wasn't checked against an effect allowlist, since the profile has no workflow.effects at RCP-CAP-006.yaml:47:9 (transitions[2].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time [Skipped]",
            ]),
        ["RCP-CAP-007"] = new(
            FixtureSetup.Capability,
            "RCP-CAP-007: capability parameter 'remote' of effect.repo.push reads fact:remote at RCP-CAP-007.yaml:22:17 (transitions[0].effects[0].with.remote); give it a literal or { profile: <key> }, never a fact",
            [
                "RCP-EFFECT-001: effect 'effect.repo.push' wasn't checked against an effect allowlist, since the profile has no workflow.effects at RCP-CAP-007.yaml:21:9 (transitions[0].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time [Skipped]",
            ]),
        ["RCP-CHILD-001"] = new(
            FixtureSetup.Shipped,
            "RCP-CHILD-001: child recipe 'workflow.absent' isn't registered or next to this recipe at RCP-CHILD-001.yaml:8:25 (states.running.waitFor.child.recipe); register it, or put workflow.absent.yaml in this recipe's directory",
            []),
        ["RCP-CHILD-002"] = new(
            FixtureSetup.Shipped,
            "RCP-CHILD-002: child recipes form a cycle: workflow.child-cycle -> workflow.child-cycle at RCP-CHILD-002.yaml:8:25 (states.running.waitFor.child.recipe); remove a child wait so no recipe starts itself, directly or through its children",
            []),
        ["RCP-CHILD-003"] = new(
            FixtureSetup.Shipped,
            "RCP-CHILD-003: child recipes nest 5 deep (workflow.depth-0 -> workflow.depth-1 -> workflow.depth-2 -> workflow.depth-3 -> workflow.depth-4 -> workflow.depth-5), more than 4 at RCP-CHILD-003.yaml:8:25 (states.running.waitFor.child.recipe); flatten the chain so children nest at most 4 deep",
            []),
        ["RCP-DUR-001"] = new(
            FixtureSetup.Shipped,
            "RCP-DUR-001: '90s' isn't a duration ('10m', '4h', '2d', at least 1m) at RCP-DUR-001.yaml:9:16 (states.produce.timeout.after); write a whole number and m, h or d, at least 1m ('10m', '4h', '2d')",
            []),
        ["RCP-EFFECT-001"] = new(
            FixtureSetup.EffectAllowlist,
            "RCP-EFFECT-001: effect 'effect.notify.desk' isn't on the profile's effect allowlist at RCP-EFFECT-001.yaml:16:83 (transitions[0].effects[0].use); add it to workflow.effects and approve the profile, or remove it",
            []),
        ["RCP-EVENT-001"] = new(
            FixtureSetup.Shipped,
            "RCP-EVENT-001: event 'item.rooted' isn't registered at RCP-EVENT-001.yaml:8:16 (states.triaged.waitFor.event); wait for a registered event: claim.created, item.routed, notify.delivered",
            []),
        ["RCP-EVID-001"] = new(
            FixtureSetup.Shipped,
            "RCP-EVID-001: evidence kind 'parity' has no registered provider (evidence.parity) at RCP-EVID-001.yaml:16:20 (transitions[0].evidence[0].kind); name a kind a registered evidence provider declares, or register evidence.parity",
            []),
        ["RCP-EXIT-001"] = new(
            FixtureSetup.Shipped,
            "RCP-EXIT-001: no transition from state 'checks' leads to a terminal state at RCP-EXIT-001.yaml:11:3 (states.checks); add a transition from it, or from a state it leads to, into a terminal state",
            []),
        ["RCP-HASH-001"] = new(
            FixtureSetup.Shipped,
            "RCP-HASH-001: hash 000000000000 doesn't match the recipe's content, whose hash is bf21a0f792f6 at RCP-HASH-001.yaml:16:1 (hash); set hash to sha256:bf21a0f792f68562707433eb71ae250767113da2997c3d497a1b654af246c4b5, or remove it",
            []),
        ["RCP-INIT-001"] = new(
            FixtureSetup.Shipped,
            "RCP-INIT-001: initial state 'done' is a terminal state at RCP-INIT-001.yaml:4:1 (initial); set initial to an active or wait state",
            []),
        ["RCP-LOOP-001"] = new(
            FixtureSetup.Shipped,
            "RCP-LOOP-001: state 'produce' names loop 'revision', which isn't declared at RCP-LOOP-001.yaml:9:5 (states.produce.loop); declare it under loops, or name a declared loop",
            []),
        ["RCP-LOOP-002"] = new(
            FixtureSetup.Shipped,
            "RCP-LOOP-002: state 'produce' is in loop 'revisions' but no transition leads back into it, so the loop never counts a second round at RCP-LOOP-002.yaml:9:5 (states.produce.loop); add the transition that leads back into it, or remove loop",
            []),
        ["RCP-LOOP-003"] = new(
            FixtureSetup.Shipped,
            "RCP-LOOP-003: loop 'revisions' has an empty noProgress at RCP-LOOP-003.yaml:18:31 (loops.revisions.noProgress); name at least one of findings, emptyDiff and sameFailure, or leave noProgress out to use all three",
            []),
        ["RCP-NEXT-001"] = new(
            FixtureSetup.Shipped,
            "RCP-NEXT-001: the next of state 'produce' uses '{ticket}', which isn't a recipe variable at RCP-NEXT-001.yaml:10:5 (states.produce.next); use only {item}, {instance}, {state}, {user}, {child}, {loop.<name>}, {loop.<name>.cap} and {budget.tokens.left}",
            []),
        ["RCP-NEXT-002"] = new(
            FixtureSetup.Shipped,
            "RCP-NEXT-002: the next of state 'produce' tells the reader to run '--grant', an approval-tier command at RCP-NEXT-002.yaml:10:5 (states.produce.next); leave it out, since only a human at a terminal runs approval-tier commands",
            []),
        ["RCP-OUTCOME-001"] = new(
            FixtureSetup.Shipped,
            "RCP-OUTCOME-001: state 'checks' has no transition for outcome 'failed' of step 'step.checks' at RCP-OUTCOME-001.yaml:11:3 (states.checks); add a transition from 'checks' on step.completed with when: { outcome: failed }",
            []),
        ["RCP-REACH-001"] = new(
            FixtureSetup.Shipped,
            "RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a transition into it or remove it",
            []),
        ["RCP-RES-001"] = new(
            FixtureSetup.Shipped,
            "RCP-RES-001: resource 'heavy' has no allocator in this build at RCP-RES-001.yaml:8:16 (states.queued.waitFor.resource); it parks with SND-WF-010 until the heavy-job lock port lands [Information]",
            []),
        ["RCP-SCHEMA-001"] = new(
            FixtureSetup.Shipped,
            "RCP-SCHEMA-001: 'retries' isn't a field of a state at RCP-SCHEMA-001.yaml:10:5 (states.produce.retries); remove it; a state has kind, step, waitFor, timeout, loop, budget, next, outcome",
            []),
        ["RCP-SCHEMA-002"] = new(
            FixtureSetup.Shipped,
            "RCP-SCHEMA-002: 'running' isn't active, wait or terminal at RCP-SCHEMA-002.yaml:7:5 (states.produce.kind); write active, wait or terminal",
            []),
        ["RCP-SCHEMA-003"] = new(
            FixtureSetup.Shipped,
            "RCP-SCHEMA-003: anchor '&standard' isn't allowed at RCP-SCHEMA-003.yaml:9:14 (states.produce.timeout); write the value out in full; recipes don't use anchors or aliases",
            []),
        ["RCP-NAME-001"] = new(
            FixtureSetup.Shipped,
            "RCP-NAME-001: state name 'produce_it' doesn't match [a-z][a-z0-9-]{0,63} at RCP-NAME-001.yaml:6:3 (states.produce_it); rename it: a lower-case letter, then up to 63 lower-case letters, digits or '-'",
            []),
        ["RCP-STATE-001"] = new(
            FixtureSetup.Shipped,
            "RCP-STATE-001: state name 'parked' is reserved for the engine at RCP-STATE-001.yaml:11:3 (states.parked); rename it; cancelled, escalated, parked and taken-over are engine-owned states",
            []),
        ["RCP-THRESH-001"] = new(
            FixtureSetup.Wiring,
            "RCP-THRESH-001: gate 'gate.review.judge' is a judge question and has no threshold at RCP-THRESH-001.yaml:17:15 (transitions[0].gates[0].threshold); add threshold: a score above 0 and at most 1, for example 0.8",
            []),
        ["RCP-THRESH-002"] = new(
            FixtureSetup.Shipped,
            "RCP-THRESH-002: gate 'gate.budget.available' is deterministic and takes no threshold at RCP-THRESH-002.yaml:16:45 (transitions[0].gates[0].threshold); remove threshold",
            []),
        ["RCP-TIME-001"] = new(
            FixtureSetup.Shipped,
            "RCP-TIME-001: active state 'produce' has no timeout at RCP-TIME-001.yaml:6:3 (states.produce.timeout); add timeout: { after, then: park | escalate | retry }",
            []),
        ["RCP-TIME-002"] = new(
            FixtureSetup.Shipped,
            "RCP-TIME-002: state 'running' retries a child wait when it times out at RCP-TIME-002.yaml:9:28 (states.running.timeout.then); use then: park or then: escalate, since a child can't be retried",
            []),
        ["RCP-TRIG-001"] = new(
            FixtureSetup.Shipped,
            "RCP-TRIG-001: trigger 'wait.satisfied' doesn't fit active state 'produce' at RCP-TRIG-001.yaml:15:32 (transitions[0].on); from an active state, use step.completed, step.failed or command.<verb>",
            []),
        ["RCP-TRIG-002"] = new(
            FixtureSetup.Shipped,
            "RCP-TRIG-002: verb 'cancel' is reserved for sindri workflow's own commands at RCP-TRIG-002.yaml:16:32 (transitions[1].on); choose another verb",
            []),
        ["RCP-WHEN-001"] = new(
            FixtureSetup.Shipped,
            "RCP-WHEN-001: transition from 'produce' tests outcome 'finished', which step 'step.produce' doesn't report at RCP-WHEN-001.yaml:16:60 (transitions[1].when.outcome); test one of its outcomes: done",
            []),
        ["RCP-WHEN-002"] = new(
            FixtureSetup.Shipped,
            "RCP-WHEN-002: transition from 'produce' tests loop 'rounds', which the recipe doesn't declare at RCP-WHEN-002.yaml:15:87 (transitions[0].when.all[1].loop); declare it under loops, or test a declared loop",
            []),
        ["RCP-WIRE-001"] = new(
            FixtureSetup.Shipped,
            "RCP-WIRE-001: step 'step.checks' in state 'checks' needs 'fact:output', but the path through transitions[0] from 'produce' doesn't produce it at RCP-WIRE-001.yaml:14:5 (states.checks.step.use); produce it on every path into 'checks', or add it to the recipe's inputs",
            []),
        ["RCP-WIRE-002"] = new(
            FixtureSetup.Shipped,
            "RCP-WIRE-002: transition from 'produce' produces 'fact:findings', which step 'step.produce' doesn't record at RCP-WIRE-002.yaml:15:88 (transitions[0].produces[0]); list only what it records (fact:output), or remove it",
            []),
    };
}
