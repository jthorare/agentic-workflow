#pragma warning disable CA1822, MA0041, MA0182 // why: the red commit's stubs have no state yet; the next commit gives them bodies
using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One editable field of the Linear issue sheet (skeleton: the red commit).</summary>
internal sealed class LinearEditFieldViewModel : ObservableObject
{
    /// <summary>The field's label.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>The value in words.</summary>
    public string Value => throw new NotSupportedException();

    /// <summary>The accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>True when the owner can edit it.</summary>
    public bool IsEditable => throw new NotSupportedException();

    /// <summary>True when the control takes input.</summary>
    public bool IsEnabled => throw new NotSupportedException();

    /// <summary>Why a picker is disabled.</summary>
    public string UnavailableText => throw new NotSupportedException();

    /// <summary>True when the draft differs from what was opened.</summary>
    public bool IsChanged => throw new NotSupportedException();

    /// <summary>The text of a text field.</summary>
    public string Text { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <summary>The choices.</summary>
    public IReadOnlyList<LinearChoiceViewModel> Options => throw new NotSupportedException();

    /// <summary>The chosen entry.</summary>
    public LinearChoiceViewModel? Selected { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    internal JsonNode? Current => throw new NotSupportedException();
}
