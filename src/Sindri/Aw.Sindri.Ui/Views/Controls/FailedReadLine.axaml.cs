using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The line every page shows for a read that failed or went stale (#3056, audit #3019): plain words, a Retry button and, only when a fix exists, a fix button.
/// It shows while its DataContext is a <see cref="FailedRead"/> and hides when that is null, so a page binds the DataContext to a nullable property and adds no visibility binding.
/// </summary>
internal sealed partial class FailedReadLine : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public FailedReadLine()
    {
        InitializeComponent();
        IsVisible = false;
        DataContextChanged += (_, _) => IsVisible = DataContext is FailedRead;   // no read to show, no line
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FailedRead read)
        {
            _ = read.Retry(CancellationToken.None);
        }
    }

    private void OnFix(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FailedRead { Fix: { } fix })
        {
            _ = fix(CancellationToken.None);
        }
    }
}
