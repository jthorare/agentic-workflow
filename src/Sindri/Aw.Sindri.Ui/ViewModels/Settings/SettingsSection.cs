namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>The sections of the Settings page the filter box shows or hides (#3362).</summary>
internal enum SettingsSection
{
    /// <summary>The setup checklist.</summary>
    Setup,

    /// <summary>The model, label and product-surface choice lines.</summary>
    Choices,

    /// <summary>The Sandbox section.</summary>
    Sandbox,

    /// <summary>The Library card.</summary>
    Library,

    /// <summary>The Connections group (Linear and the Obsidian todo note).</summary>
    Connections,

    /// <summary>"This app": Reduce motion and Text size.</summary>
    ThisApp,
}
