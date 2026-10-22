namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Where the run page's read stands (F3 spec 3 §3, "Page states").</summary>
internal enum RunPageState
{
    /// <summary>Nothing was asked for yet.</summary>
    Empty,

    /// <summary>The route is open and the run is not read.</summary>
    Loading,

    /// <summary>A run is shown.</summary>
    Shown,

    /// <summary>The run is not served.</summary>
    NotFound,

    /// <summary>The answer could not be read, or Sindri refused it.</summary>
    Problem,
}
