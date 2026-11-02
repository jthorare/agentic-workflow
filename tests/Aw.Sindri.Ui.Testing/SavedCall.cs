using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Testing;

/// <summary>One save the fake was asked for.</summary>
/// <param name="Save">The pages and removals.</param>
/// <param name="IfHash">The hash the sheet sent.</param>
/// <param name="Approve">Whether the sheet asked to approve.</param>
internal sealed record SavedCall(PageSave Save, string? IfHash, bool Approve);
