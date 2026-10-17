namespace Aw.Sindri.Core.Views;

/// <summary>The view schema version: bump it when a projection's fold changes, and the projector rebuilds.</summary>
public static class ViewSchema
{
    /// <summary>The version this build folds with (6: API P10a's launches view; 5: API P11's engine item states; 4: API P6's jobs view and the failure row's new cli; 3: API P4's request, decision and needs-you views).</summary>
    public const int Version = 6;
}
