using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// An effect's stable identity (spec §5.7): '{instance}:{entry}:{index}' with the entered frame's entry, so an
/// activity retry or a 'retry' re-emission is harmless to the effect's existence check. 'effect.claim.ref'
/// keys its identity by item instead (P6). An engine notice (§5.7 origin 'engine'; P6 unit (c)) has no index in a
/// transition's effects: it is '{instance}:{entry}:n{version}', so two notices in one entry stay distinct.
/// </summary>
/// <param name="Instance">The instance.</param>
/// <param name="Entry">The entry of the frame the transition enters.</param>
/// <param name="Index">The effect's index in the transition's effects; 0 for an engine notice.</param>
/// <param name="Notice">An engine notice's instance version; null for a recipe effect.</param>
public sealed record EffectIdentity(WorkflowInstanceId Instance, int Entry, int Index, int? Notice = null)
{
    /// <summary>'{instance}:{entry}:{index}', or '{instance}:{entry}:n{version}' for an engine notice.</summary>
    public string Text => Notice is { } version
        ? string.Create(CultureInfo.InvariantCulture, $"{Instance.Value}:{Entry}:n{version}")
        : string.Create(CultureInfo.InvariantCulture, $"{Instance.Value}:{Entry}:{Index}");

    /// <summary>The identity of the engine notice the instance sends at <paramref name="version"/> in <paramref name="entry"/>.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="entry">Its frame's entry.</param>
    /// <param name="version">Its version after the change that sends the notice.</param>
    /// <returns>The identity.</returns>
    public static EffectIdentity ForNotice(WorkflowInstanceId instance, int entry, int version) => new(instance, entry, Index: 0, version);
}
