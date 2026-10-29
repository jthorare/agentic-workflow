using Avalonia.Automation;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Sandbox;

/// <summary>Settings' Sandbox section (sandbox spec 8); binds to <see cref="ViewModels.Sandbox.SandboxSettingsViewModel"/>.</summary>
internal sealed partial class SandboxSettingsSection : UserControl
{
    public SandboxSettingsSection()
    {
        InitializeComponent();
        AutomationProperties.SetName(SandboxSection, Labels.SandboxHeading);   // why: named before the view model arrives, so a reader never meets a nameless part
        AutomationProperties.SetName(SandboxTitle, Labels.SandboxHeading);
        AutomationProperties.SetName(SandboxFieldsCard, Labels.SandboxFieldsName);
        AutomationProperties.SetName(SandboxFields, Labels.SandboxFieldsName);
    }
}
