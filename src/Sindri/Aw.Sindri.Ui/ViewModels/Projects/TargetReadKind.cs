namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>What <see cref="TargetDateReader"/> made of the text in the Target box.</summary>
internal enum TargetReadKind
{
    /// <summary>The box is empty or blank: no target.</summary>
    Empty,

    /// <summary>The text read as a date.</summary>
    Date,

    /// <summary>The text is not a date the reader knows.</summary>
    Unreadable,
}
