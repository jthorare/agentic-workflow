namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The owner's answer about hooks in the sandbox (2026-10-18; spike item 6): hooks help performance and quality, so the default is to keep them.
/// Only a hook that weakens the sandbox is treated (<see cref="HookInventory.Scripts"/> holds each treatment).
/// </summary>
public static class HookPermissionPolicy
{
    /// <summary>The treatment of one hook: its inventory row, or <see cref="HookTreatment.Keep"/> for a hook the inventory doesn't list.</summary>
    /// <param name="hook">The hook's name.</param>
    /// <returns>The treatment.</returns>
    public static HookTreatment TreatmentOf(string hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return HookInventory.Find(hook)?.Treatment ?? HookTreatment.Keep;
    }

    /// <summary>The hooks a sandboxed session keeps and leaves out.</summary>
    /// <param name="installed">The hooks installed on the box, or null for the inventory itself.</param>
    /// <returns>The kept and omitted names, in inventory order then the unlisted ones, with a note for every installed hook the inventory doesn't list.</returns>
    public static PlanHooks ForSession(IReadOnlyList<string>? installed = null)
    {
        var names = installed is null ? HookInventory.Scripts.Select(s => s.Name).ToList() : [.. installed.Distinct(StringComparer.Ordinal)];
        var listed = HookInventory.Scripts.Select(s => s.Name).Where(n => names.Contains(n, StringComparer.Ordinal)).ToList();
        var unlisted = names.Where(n => HookInventory.Find(n) is null).ToList();
        return new PlanHooks(
            [.. listed.Where(n => TreatmentOf(n) != HookTreatment.Drop), .. unlisted],
            [.. listed.Where(n => TreatmentOf(n) == HookTreatment.Drop)],
            [.. unlisted.Select(n => $"hook '{n}' is not in the sandbox's hook inventory, so it is kept (the default); add it to HookInventory with its treatment")]);
    }
}
