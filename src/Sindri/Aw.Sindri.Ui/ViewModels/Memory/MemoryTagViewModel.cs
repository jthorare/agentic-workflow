namespace Aw.Sindri.Ui.ViewModels.Memory;

/// <summary>One tag of a fact, with the action that forgets every fact carrying it.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Forget">Sends the forget for this tag.</param>
internal sealed record MemoryTagViewModel(string Tag, Func<CancellationToken, Task> Forget)
{
    /// <summary>The action's words: "Forget all tagged deploys".</summary>
    public string ForgetText => Labels.MemoryForgetTag(Tag);
}
