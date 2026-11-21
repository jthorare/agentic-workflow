using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>What <see cref="RowKeys"/>' peek, open and back events carry (#2978).</summary>
internal sealed class RowKeyEventArgs : RoutedEventArgs
{
    /// <summary>The index of the cursor row in the list's items.</summary>
    public int Index { get; init; }

    /// <summary>True when the key is held down (the peek shows until it comes up), false for a tap.</summary>
    public bool Hold { get; init; }

    /// <summary>True when the held key came up.</summary>
    public bool Released { get; init; }
}
