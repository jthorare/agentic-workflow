#pragma warning disable CA1822, MA0041, MA0182 // why: the red commit's stubs have no state yet; the next commit gives them bodies
namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One field of a 409 (skeleton: the red commit).</summary>
internal sealed class LinearConflictRowViewModel
{
    /// <summary>The field's label.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>"Linear: X · yours: Y".</summary>
    public string Text => throw new NotSupportedException();

    /// <summary>Takes Linear's value for this field.</summary>
    internal void UseLinears() => throw new NotSupportedException();
}
