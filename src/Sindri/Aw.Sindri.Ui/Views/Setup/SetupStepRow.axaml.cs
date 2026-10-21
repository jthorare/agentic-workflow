using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the checklist's row template places it in XAML, which the analyzer can't see
/// <summary>One step of the checklist: it draws the step view model and holds no logic beyond naming the header's toggle.</summary>
internal sealed partial class SetupStepRow : UserControl
{
    private SetupStepViewModel? _step;
    private ToggleButton? _toggle;

    public SetupStepRow()
    {
        InitializeComponent();
        SetupStepExpander.TemplateApplied += OnTemplateApplied;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _step?.PropertyChanged -= OnStepChanged;
        _step = DataContext as SetupStepViewModel;
        _step?.PropertyChanged += OnStepChanged;
        NameToggle();
    }

    // The header's toggle is the row's one control and its content is raw (the lines are read once, in the row's name), so it takes that name: the title first.
    private void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _toggle = SetupStepExpander.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault();
        NameToggle();
    }

    private void OnStepChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SetupStepViewModel.Name), StringComparison.Ordinal))
        {
            NameToggle();
        }
    }

    private void NameToggle()
    {
        if (_toggle is not null)
        {
            AutomationProperties.SetName(_toggle, _step?.Name);
        }
    }
}
#pragma warning restore MA0182
