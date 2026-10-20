using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'workflow:' block (engine spec §7.2 S1-D11; engine P4 adds 'deskTimeout' and 'eventHold', P6 'effects'
/// and 'dispatchDelay', P7 theirs). Each time is a duration ('1m', '4h', '2d'); a key left out takes its default
/// (<see cref="WorkflowTimes.Defaults"/>, <see cref="EffectSettings.DefaultDispatchDelay"/>).
/// </summary>
/// <param name="DeskTimeout">The floor of every desk Step's timeout (W20; default 2d). The dev profile sets 1m so a desk wait parks fast.</param>
/// <param name="EventHold">How long a buffered event signal waits for its wait before it is dropped with 'workflow.expired' (§5.4; default 24h).</param>
/// <param name="Effects">The effect allowlist (§5.7, RCP-EFFECT-001): the 'effect.*' ids a recipe may dispatch. Absent: RCP-EFFECT-001 is skipped and every recipe effect is refused at dispatch (SND-WF-011). Engine P6 unit (d) checks it.</param>
/// <param name="DispatchDelay">The undo window before an effect's activity runs (§5.7; default 10s; the dev profile sets 60s). Engine P6 unit (d) checks it.</param>
/// <param name="Agent">The 'agent:' section (engine P8): the agent a Step launches (<see cref="WorkflowAgentSettings"/>).</param>
/// <param name="Judge">The 'judge:' section (DT-J plan Q8): which providers a Judge Step may ask (<see cref="JudgeProviders"/>).</param>
/// <param name="Forge">The forge port's settings (PR babysitter spec W27): adapter, hosts and credential source.</param>
/// <param name="Route">The 'route:' section (engine P7 plan D8): whether the router stub lets routed items start (<see cref="RouteSettings"/>).</param>
/// <param name="Packs">The 'packs:' section (line spec §5): the style and lens packs and their pins.</param>
/// <param name="Line">The 'line:' section (line spec §8): parallel phases, open pull requests and stack depth.</param>
/// <param name="Plan">The 'plan:' section (line spec §6.3): the size classes and the largest phase size.</param>
/// <param name="Prove">The 'prove:' section (line spec §7.3): the hidden-scenario minimum.</param>
/// <param name="Build">The 'build:' section (line spec §7.2): the commands that check a change.</param>
/// <param name="Ship">The 'ship:' section (line spec §7.5): draft pull requests.</param>
/// <param name="Design">The 'design:' section (line spec §4): the design and lens models.</param>
/// <param name="Review">The 'review:' section (line spec §7.4): the review lens model.</param>
/// <param name="Steps">The 'steps:' section (line spec LN-WF7): the longest block Step.</param>
/// <param name="Docs">The 'docs:' section (line spec §7.6): where docs sync may write.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkflowSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeskTimeout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EventHold = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Effects = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DispatchDelay = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JudgeSettings? Judge = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkflowAgentSettings? Agent = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ForgeSettings? Forge = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RouteSettings? Route = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PackSettings? Packs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LineSettings? Line = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PlanSettings? Plan = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProveSettings? Prove = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildSettings? Build = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ShipSettings? Ship = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DesignSettings? Design = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewSettings? Review = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StepsSettings? Steps = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DocsSettings? Docs = null);
