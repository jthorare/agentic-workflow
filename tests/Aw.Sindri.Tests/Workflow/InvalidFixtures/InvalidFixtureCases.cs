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
        ["RCP-CAP-003"] = new(
            FixtureSetup.CapabilityWithUnapprovedBase,
            "RCP-CAP-003: base workflow.cap-base@b8f01b2b6c69 is registered but not approved at RCP-CAP-003.yaml:4:1 (base); approve the base at a terminal (sindri recipe approve workflow.cap-base@b8f01b2b6c69), or generate the recipe from an approved base",
            [
                "RCP-EFFECT-001: effect 'effect.repo.push' wasn't checked against an effect allowlist, since the profile has no workflow.effects at RCP-CAP-003.yaml:47:9 (transitions[2].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time [Skipped]",
            ]),
        ["RCP-CAP-004"] = new(
            FixtureSetup.Capability,
            "RCP-CAP-004: hand-written recipe workflow.cap-handwritten names base workflow.cap-base@b8f01b2b6c69 at RCP-CAP-004.yaml:4:1 (base); remove base, which only Sindri's generator writes",
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
            "RCP-CHILD-003: child recipes nest 6 deep (workflow.depth-0 -> workflow.depth-1 -> workflow.depth-2 -> workflow.depth-3 -> workflow.depth-4 -> workflow.depth-5 -> workflow.depth-6), more than 5 at RCP-CHILD-003.yaml:8:25 (states.running.waitFor.child.recipe); flatten the chain so children nest at most 5 deep",
            []),
        ["RCP-DUR-001"] = new(
            FixtureSetup.Shipped,
            "RCP-DUR-001: '90s' isn't a duration ('10m', '4h', '2d', at least 1m) at RCP-DUR-001.yaml:9:16 (states.produce.timeout.after); write a whole number and m, h or d, at least 1m ('10m', '4h', '2d')",
            []),
        ["RCP-EFFECT-001"] = new(
            FixtureSetup.EffectAllowlist,
            "RCP-EFFECT-001: effect 'effect.notify.desk' isn't on the profile's effect allowlist at RCP-EFFECT-001.yaml:16:83 (transitions[0].effects[0].use); add it to workflow.effects and approve the profile, or remove it",
            []),
        ["RCP-HUMAN-001"] = new(
            FixtureSetup.HumanTrigger,
            "RCP-HUMAN-001: effect 'effect.test.merge' acts with a human's authority, but transitions[0] (ask -> done on step.completed) isn't a human's act at RCP-HUMAN-001.yaml:16:83 (transitions[0].effects[0].use); put it on a command.<verb> transition, a step.completed out of a human-only Step, or a transition that requires a fact only such a transition produces",
            []),
        ["RCP-HUMAN-001-fact"] = new(
            FixtureSetup.HumanTrigger,
            "RCP-HUMAN-001: effect 'effect.test.merge' acts with a human's authority, but transitions[1] (hold -> done on wait.satisfied) isn't a human's act at RCP-HUMAN-001-fact.yaml:22:101 (transitions[1].effects[0].use); put it on a command.<verb> transition, a step.completed out of a human-only Step, or a transition that requires a fact only such a transition produces",
            []),
        ["RCP-HUMAN-001-input"] = new(
            FixtureSetup.HumanTrigger,
            "RCP-HUMAN-001: effect 'effect.test.merge' acts with a human's authority, but transitions[3] (tidy -> done on step.completed) isn't a human's act at RCP-HUMAN-001-input.yaml:29:135 (transitions[3].effects[0].use); put it on a command.<verb> transition, a step.completed out of a human-only Step, or a transition that requires a fact only such a transition produces",
            []),
        ["RCP-HUMAN-001-step"] = new(
            FixtureSetup.HumanTrigger,
            "RCP-HUMAN-001: effect 'effect.test.merge' acts with a human's authority, but transitions[4] (tidy -> done on step.completed) isn't a human's act at RCP-HUMAN-001-step.yaml:36:135 (transitions[4].effects[0].use); put it on a command.<verb> transition, a step.completed out of a human-only Step, or a transition that requires a fact only such a transition produces",
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
            "RCP-HASH-001: hash 000000000000 doesn't match the recipe's content, whose hash is b2371052fdcf at RCP-HASH-001.yaml:16:1 (hash); set hash to sha256:b2371052fdcf9ed4a8dd9019036d5f3725abf3e5ec115746d3d47591cc72706e, or remove it",
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
            "RCP-RES-001: resource 'heavy' has no allocator in this build at RCP-RES-001.yaml:8:16 (states.queued.waitFor.resource); it parks with SND-WF-010 until the workflow engine's heavy allocator is wired to the heavy-job lock [Information]",
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
        ["RCP-TIME-003"] = new(
            FixtureSetup.Shipped,
            "RCP-TIME-003: state 'route' runs step.judge.choose on the block runtime, where 'then: retry' doesn't apply at RCP-TIME-003.yaml:16:27 (states.route.timeout.then); use then: park (a judge call that runs out ends as step.failed, which the route sends to its desk twin)",
            []),
        ["RCP-TIME-003-short"] = new(
            FixtureSetup.Shipped,
            "RCP-TIME-003: state 'route' times out after 1 min but step.judge.choose may run for 2 min, so the state's timer and the Step's own bound would race at RCP-TIME-003-short.yaml:16:16 (states.route.timeout.after); make the state timeout longer than the Step's maxDuration (2 min)",
            []),
        ["RCP-TRIG-001"] = new(
            FixtureSetup.Shipped,
            "RCP-TRIG-001: trigger 'wait.satisfied' doesn't fit active state 'produce' at RCP-TRIG-001.yaml:15:32 (transitions[0].on); from an active state, use step.completed, step.failed or command.<verb>",
            []),
        ["RCP-TRIG-002"] = new(
            FixtureSetup.Shipped,
            "RCP-TRIG-002: verb 'cancel' is reserved for sindri workflow's own commands at RCP-TRIG-002.yaml:16:32 (transitions[1].on); choose another verb",
            []),
        ["RCP-VERSION-001"] = new(
            FixtureSetup.Registry,
            "RCP-VERSION-001: workflow.version-taken version 1 is registered with hash 2bd784957be7, but this recipe's hash is a993db8e5966 at RCP-VERSION-001.yaml:3:1 (version); raise version to 2, one above the highest registered, or restore the content registered as version 1",
            []),
        ["RCP-VERSION-002"] = new(
            FixtureSetup.Registry,
            "RCP-VERSION-002: workflow.version-behind version 2 isn't registered and is below version 3, the highest registered at RCP-VERSION-002.yaml:3:1 (version); raise version to 4, one above the highest registered",
            []),
        ["RCP-WHEN-001"] = new(
            FixtureSetup.Shipped,
            "RCP-WHEN-001: transition from 'produce' tests outcome 'finished', which step 'step.produce' doesn't report (outcomes here: done) at RCP-WHEN-001.yaml:16:60 (transitions[1].when.outcome); test one of its outcomes: done",
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
        ["RCP-WIRE-003"] = new(
            FixtureSetup.Shipped,
            "RCP-WIRE-003: transition from 'run' produces 'fact:output' on outcome 'unresolved' of child 'workflow.dev-child-summary', which doesn't produce 'fact:output' on every path to 'unresolved' (in workflow.dev-child-summary, the path through transitions[4] from 'export' doesn't produce it) at RCP-WIRE-003.yaml:12:93 (transitions[1].produces[0]); produce it on that path in workflow.dev-child-summary, or produce it here only on outcomes that always carry it (resolved)",
            []),
        ["RCP-WIRE-003-cancelled"] = new(
            FixtureSetup.Shipped,
            "RCP-WIRE-003: transition from 'run' produces 'fact:output' on outcome 'cancelled' of child 'workflow.dev-child-summary', which carries no outputs at RCP-WIRE-003-cancelled.yaml:13:92 (transitions[2].produces[0]); route cancelled with a transition of its own that produces nothing",
            []),
        ["RCP-WIRE-002-child"] = new(
            FixtureSetup.Shipped,
            "RCP-WIRE-002: transition from 'run' produces 'fact:findings', which child 'workflow.dev-child-summary' doesn't declare as an output at RCP-WIRE-002-child.yaml:11:88 (transitions[0].produces[0]); list only its outputs (fact:output), or declare 'fact:findings' in workflow.dev-child-summary's outputs",
            []),
        ["RCP-WIRE-002-rename"] = new(
            FixtureSetup.Shipped,
            "RCP-WIRE-002: transition from 'produce' renames 'fact:output' to 'fact:exported', but only a child's outputs can be renamed at RCP-WIRE-002-rename.yaml:15:88 (transitions[0].produces[0]); write the bare key 'fact:output', or rename on a transition from a child wait",
            []),
        ["RCP-SCHEMA-001-produces"] = new(
            FixtureSetup.Shipped,
            "RCP-SCHEMA-001: a produces entry has no 'from' at RCP-SCHEMA-001-produces.yaml:15:88 (transitions[0].produces[0].from); add 'from': the child's output key, or write the bare key",
            []),
        ["RCP-URG-001"] = new(
            FixtureSetup.Shipped,
            "RCP-URG-001: urgency 'urgent' isn't one of incident, triage, work at RCP-URG-001.yaml:4:1 (urgency); use incident, triage or work, or remove urgency (the default is work)",
            []),
        ["RCP-OUT-001"] = new(
            FixtureSetup.Shipped,
            "RCP-OUT-001: output 'fact:output' comes from state 'draft', which the recipe doesn't declare at RCP-OUT-001.yaml:5:37 (outputs[0].from); name one of its states: produce, done",
            []),
        ["RCP-OUT-002"] = new(
            FixtureSetup.Shipped,
            "RCP-OUT-002: output 'fact:report' comes from state 'produce', but nothing there records it (step 'step.produce' records fact:output) at RCP-OUT-002.yaml:5:37 (outputs[0].from); have the state's Step, a transition leaving it ('produces') or its wait record 'fact:report', or name the state that does",
            []),
        ["RCP-OUT-003"] = new(
            FixtureSetup.Shipped,
            "RCP-OUT-003: output key 'fact:output' is declared twice (first at outputs[0]) at RCP-OUT-003.yaml:6:7 (outputs[1].key); remove the duplicate, or give it its own key",
            []),
        ["RCP-TYPE-001"] = new(
            FixtureSetup.Shipped,
            "RCP-TYPE-001: output 'fact:output' has type 'document', which isn't one of text, file, item, link, scope-map, packet, evidence, decision, payload at RCP-TYPE-001.yaml:5:25 (outputs[0].type); use one of them",
            []),
        ["RCP-TYPE-002"] = new(
            FixtureSetup.Shipped,
            "RCP-TYPE-002: input 'fact:item' has kind 'ticket', which isn't one of item, linear-project, webhook, file, text, link, packet, payload at RCP-TYPE-002.yaml:5:35 (inputs[0].kind); use one of them",
            []),
        ["RCP-TYPE-003"] = new(
            FixtureSetup.Shipped,
            "RCP-TYPE-003: input 'fact:item' has kind 'item' but type 'file'; kind item pairs with type item at RCP-TYPE-003.yaml:5:35 (inputs[0].kind); set type to item, or change kind",
            []),
        ["RCP-INPUT-001"] = new(
            FixtureSetup.Shipped,
            "RCP-INPUT-001: input 'fact:item' declares no kind at RCP-INPUT-001.yaml:5:5 (inputs[0]); add kind: item | link | linear-project | webhook | file | text | packet | payload, paired with its type (item/item, file/file, text/text, link/link, packet/packet; payload, linear-project and webhook pair with payload)",
            []),
        ["RCP-INPUT-002"] = new(
            FixtureSetup.Shipped,
            "RCP-INPUT-002: the recipe declares no input at RCP-INPUT-002.yaml:1:1 (inputs); declare at least one input; a scheduled recipe gives it generate: \"<template>\"",
            []),
        ["RCP-NAME-002"] = new(
            FixtureSetup.Shipped,
            "RCP-NAME-002: the name template is empty or longer than 200 characters at RCP-NAME-002.yaml:4:1 (name); write literal text with {fact:<input>} or {start:date|time}",
            []),
        ["RCP-NAME-003"] = new(
            FixtureSetup.Shipped,
            "RCP-NAME-003: unknown placeholder '{owner}' at RCP-NAME-003.yaml:4:1 (name); use {fact:<input>}, {start:date} or {start:time}, and write {{ or }} for a literal brace",
            []),
        ["RCP-NAME-004"] = new(
            FixtureSetup.Shipped,
            "RCP-NAME-004: '{fact:scope}' names no declared input at RCP-NAME-004.yaml:4:1 (name); declare fact:scope under inputs or remove the placeholder",
            []),
        ["RCP-NAME-005"] = new(
            FixtureSetup.Shipped,
            "RCP-NAME-005: generate may not read inputs ('{fact:note}') at RCP-NAME-005.yaml:5:47 (inputs[0].generate); use only literals, {start:date} and {start:time}",
            []),
        ["RCP-NAME-006"] = new(
            FixtureSetup.Shipped,
            "RCP-NAME-006: generate on an input of kind 'item' at RCP-NAME-006.yaml:5:47 (inputs[0].generate); only a text input can be generated; supply this input at start",
            []),
        ["RCP-CHOICE-001"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-001: state 'route' gives 'step.judge.choose' 1 choices at RCP-CHOICE-001.yaml:13:9 (states.route.step.with.choices); list 2 to 32 choices, each \"<outcome>: <criterion>\"",
            []),
        ["RCP-CHOICE-002"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-002: entry 1 of choices in state 'route' has no ': ' after its outcome at RCP-CHOICE-002.yaml:14:13 (states.route.step.with.choices[0]); write it as \"<outcome>: <criterion>\"",
            []),
        ["RCP-CHOICE-002-mapping"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-002: entry 1 of choices in state 'route' is a YAML mapping at RCP-CHOICE-002-mapping.yaml:14:13 (states.route.step.with.choices[0]); quote it: \"<key>: <value>\"",
            []),
        ["RCP-CHOICE-003"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-003: entries 1 and 2 of choices in state 'route' both name outcome 'api' at RCP-CHOICE-003.yaml:15:13 (states.route.step.with.choices[1]); give each choice its own outcome",
            []),
        ["RCP-CHOICE-004"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-004: state 'route' gives 'step.judge.choose' no facts to read at RCP-CHOICE-004.yaml:12:9 (states.route.step.with.facts); list at least one fact key under facts",
            []),
        ["RCP-CHOICE-005"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-005: state 'route' routes uncertain to 'prove', which isn't its desk twin at RCP-CHOICE-005.yaml:32:5 (transitions[2]); route it to a state with step: { use: step.desk.choose, with: { from: route } }",
            []),
        ["RCP-CHOICE-005-computed"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-005: from of state 'route-desk' is a { profile } value at RCP-CHOICE-005-computed.yaml:20:44 (states.route-desk.step.with.from); name the route state it twins as a literal: from: <state>",
            []),
        ["RCP-CHOICE-006"] = new(
            FixtureSetup.Shipped,
            "RCP-CHOICE-006: transitions[0] routes 'route' on step.failed before its branch transitions[1] at RCP-CHOICE-006.yaml:30:5 (transitions[0]); list the branch transitions first and the uncertain and step.failed ones last",
            []),
        ["RCP-OUTCOME-001-uncertain"] = new(
            FixtureSetup.Shipped,
            "RCP-OUTCOME-001: state 'route' has no transition for outcome 'uncertain' of step 'step.judge.choose' at RCP-OUTCOME-001-uncertain.yaml:6:3 (states.route); add a transition from 'route' on step.completed with when: { outcome: uncertain }",
            []),
        ["RCP-SCHEMA-002-camel"] = new(
            FixtureSetup.Shipped,
            "RCP-SCHEMA-002: 'fact:evidence-summary' isn't a fact key ('fact:<name>' or 'evidence:<kind>@<base|head>') at RCP-SCHEMA-002-camel.yaml:34:86 (transitions[4].produces[0]); fact names are camelCase: write fact:evidenceSummary",
            []),
    };
}
