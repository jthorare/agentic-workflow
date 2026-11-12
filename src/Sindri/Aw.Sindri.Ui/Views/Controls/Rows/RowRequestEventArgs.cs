using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The row a <see cref="RowList"/> raised PeekRequested, OpenRequested or BackRequested for.</summary>
internal sealed class RowRequestEventArgs : RoutedEventArgs
{
    /// <summary>The row's item, or null when the list has no cursor row.</summary>
    public object? Item { get; init; }

    /// <summary>True when the peek is held open by a held key, false for a tap toggle.</summary>
    public bool Hold { get; init; }

    /// <summary>True when the held key came up.</summary>
    public bool Released { get; init; }
}
