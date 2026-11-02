using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>
/// Hosts a child that is built the first time this control is measured, not when the XAML around it loads. A control
/// that is not visible is never measured, so a hidden page costs nothing until it is shown; a window that opens on one
/// page no longer styles all the others. The child inherits this control's <see cref="StyledElement.DataContext"/>.
/// Names declared in the <see cref="Template"/> belong to the template's own name scope, not the window's, so find the
/// built child through the visual tree.
/// </summary>
internal sealed class Deferred : Decorator
{
    /// <summary>Gets or sets the template that builds the child; it is called once, with no data.</summary>
    public IDataTemplate? Template { get; set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null && Template?.Build(param: null) is { } built)
        {
            Child = built;
        }

        return base.MeasureOverride(availableSize);
    }
}
#pragma warning restore MA0182
