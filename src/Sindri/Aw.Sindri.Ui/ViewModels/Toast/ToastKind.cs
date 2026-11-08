namespace Aw.Sindri.Ui.ViewModels.Toast;

/// <summary>What a toast is for (todo spec §3.3): a plain one, or a pick's, which is never replaced and never pauses.</summary>
internal enum ToastKind
{
    /// <summary>A short note, maybe with Undo; a newer toast replaces it, and hovering or focusing it pauses its timer when it has Undo.</summary>
    Normal,

    /// <summary>A conflict or hold pick's Undo window: it counts down on the clock, never pauses, and a newer toast waits behind it.</summary>
    Pick,
}
