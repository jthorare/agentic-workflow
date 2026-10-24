using System.Text.Json.Serialization;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Scripts;

/// <summary>
/// One scripted browser step and the state that must hold after it. JSON reads through the constructor with every optional
/// member defaulted, so a press may omit its target; <see cref="ScriptReader"/> checks which members belong to which action.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScriptStep
{
    /// <summary>A step with a target and no value (a goto, a click, an expect-visible).</summary>
    /// <param name="action">What the step does.</param>
    /// <param name="target">A path for goto, a step target for click, visible text for expect-visible.</param>
    /// <param name="expectedState">The state checked after the action.</param>
    public ScriptStep(StepAction action, string target, ExpectedState expectedState)
        : this(action, expectedState, target)
    {
    }

    /// <summary>A step with a target and a value (a fill, or a scroll's position).</summary>
    /// <param name="action">What the step does.</param>
    /// <param name="target">A step target for fill, a test id for scroll.</param>
    /// <param name="expectedState">The state checked after the action.</param>
    /// <param name="value">The text a fill types, or a scroll's position; null types nothing.</param>
    public ScriptStep(StepAction action, string target, ExpectedState expectedState, string? value)
        : this(action, expectedState, target, value)
    {
    }

    /// <summary>A step with every member; JSON reads through this constructor.</summary>
    /// <param name="action">What the step does.</param>
    /// <param name="expectedState">The state checked after the action.</param>
    /// <param name="target">The element or path; a press may omit it.</param>
    /// <param name="value">The text a fill types, or a scroll's position.</param>
    /// <param name="index">Picks the nth match (0-based) when a test id repeats; click, fill, scroll and press only.</param>
    /// <param name="to">A drag's destination test id.</param>
    /// <param name="grabOffset">Where on the source a drag takes hold, in pixels from its top-left corner; default its center.</param>
    /// <param name="dropOffset">Where on the destination a drag lets go, in pixels from its top-left corner; default its center.</param>
    /// <param name="holdAlt">Whether a drag holds Alt through the release.</param>
    /// <param name="key">A press's key (a Playwright key such as 'ArrowRight' or 'Shift+Tab').</param>
    [JsonConstructor]
    public ScriptStep(
        StepAction action,
        ExpectedState expectedState,
        string? target = null,
        string? value = null,
        int? index = null,
        string? to = null,
        Offset? grabOffset = null,
        Offset? dropOffset = null,
        bool? holdAlt = null,
        string? key = null)
    {
        Action = action;
        Target = target ?? string.Empty;
        ExpectedState = expectedState;
        Value = value;
        Index = index;
        To = to;
        GrabOffset = grabOffset;
        DropOffset = dropOffset;
        HoldAlt = holdAlt;
        Key = key;
    }

    /// <summary>What the step does.</summary>
    public StepAction Action { get; }

    /// <summary>A path for goto, a step target for click and fill, a test id for scroll and drag, visible text for expect-visible; empty for a press with no target.</summary>
    public string Target { get; }

    /// <summary>The state checked after the action.</summary>
    [JsonConverter(typeof(KindFirstConverter))]
    public ExpectedState ExpectedState { get; }

    /// <summary>The text a fill types, or a scroll's position ('end' by default).</summary>
    public string? Value { get; }

    /// <summary>Picks the nth match (0-based) when a test id repeats; click, fill, scroll and press only.</summary>
    public int? Index { get; }

    /// <summary>A drag's destination test id.</summary>
    public string? To { get; }

    /// <summary>Where on the source a drag takes hold, in pixels from its top-left corner; null is its center.</summary>
    public Offset? GrabOffset { get; }

    /// <summary>Where on the destination a drag lets go, in pixels from its top-left corner; null is its center.</summary>
    public Offset? DropOffset { get; }

    /// <summary>Whether a drag holds Alt through the release.</summary>
    public bool? HoldAlt { get; }

    /// <summary>A press's key.</summary>
    public string? Key { get; }

    /// <summary>The action's name as scripts write it.</summary>
    [JsonIgnore]
    public string ActionName => NameOf(Action);

    /// <summary>What the step acts on, as summaries and errors show it: 'card → lane' for a drag, 'ArrowRight on tab' for a press, else the target.</summary>
    [JsonIgnore]
    public string Subject => Action switch
    {
        StepAction.Drag => $"{Target} → {To}",
        StepAction.Press => Target.Length == 0 ? Key ?? string.Empty : $"{Key} on {Target}",
        StepAction.Goto or StepAction.Click or StepAction.Fill or StepAction.ExpectVisible or StepAction.Scroll => Target,
        _ => Target,
    };

    /// <summary>An action's name as scripts write it.</summary>
    /// <param name="action">The action.</param>
    /// <returns>'goto', 'click', 'fill', 'expect-visible', 'scroll', 'drag' or 'press'.</returns>
    public static string NameOf(StepAction action) => action switch
    {
        StepAction.Goto => "goto",
        StepAction.Click => "click",
        StepAction.Fill => "fill",
        StepAction.ExpectVisible => "expect-visible",
        StepAction.Scroll => "scroll",
        StepAction.Drag => "drag",
        StepAction.Press => "press",
        _ => "unknown",
    };

    /// <summary>A description of the step and its expectation, for the selector-repair prompt.</summary>
    /// <returns>The description.</returns>
    public string Describe() => $"{ActionName} {Subject}; expected: {ExpectedState.Describe()}";
}
