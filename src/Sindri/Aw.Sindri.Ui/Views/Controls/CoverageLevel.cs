namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>How fully an area was searched.</summary>
internal enum CoverageLevel
{
    /// <summary>Searched in full.</summary>
    Searched,

    /// <summary>Searched in part.</summary>
    Limited,

    /// <summary>Not searched at all.</summary>
    NotAvailable,
}
