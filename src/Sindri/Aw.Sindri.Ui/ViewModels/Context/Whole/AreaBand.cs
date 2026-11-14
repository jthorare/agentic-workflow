namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A band of an area's canvas.</summary>
internal enum AreaBand
{
    /// <summary>Routes, jobs, middleware.</summary>
    Entry,

    /// <summary>Services and domain logic.</summary>
    Core,

    /// <summary>Tables, events.</summary>
    Data,
}
