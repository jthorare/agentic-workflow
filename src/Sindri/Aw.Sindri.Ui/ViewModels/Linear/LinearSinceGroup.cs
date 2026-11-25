namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Which fixed group of the since-you-looked card an issue's row sits in. An issue with several kinds of change sits in the first group that fits.</summary>
internal enum LinearSinceGroup
{
    /// <summary>The issue was assigned to the viewer.</summary>
    Assigned,

    /// <summary>The issue changed state (and was not assigned).</summary>
    Moved,

    /// <summary>Anything else: a mention.</summary>
    Other,
}
