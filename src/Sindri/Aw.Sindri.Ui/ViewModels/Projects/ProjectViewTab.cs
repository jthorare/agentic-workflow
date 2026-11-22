using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One view's tab in the Projects page's segmented control: its words, its count, its key and whether it is the view shown.</summary>
internal sealed class ProjectViewTab : ObservableObject
{
    internal ProjectViewTab(ProjectView view, string text)
    {
        (View, Text) = (view, text);
    }

    /// <summary>The view.</summary>
    internal ProjectView View { get; }

    /// <summary>The tab's words.</summary>
    public string Text { get; }

    /// <summary>The key that shows it: "1" to "4".</summary>
    public string Key => ((int)View).ToString(CultureInfo.InvariantCulture);

    /// <summary>How many projects the view holds.</summary>
    public int Count
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(CountText));
            OnChanged(nameof(Name));
        }
    }

    /// <summary>The count as drawn.</summary>
    public string CountText => Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>The tab's accessible name: "Needs me, 6 projects".</summary>
    public string Name => Labels.ProjectsViewName(Text, Count);

    /// <summary>True while it is the view shown.</summary>
    public bool IsOn { get; internal set => Set(ref field, value); }
}
