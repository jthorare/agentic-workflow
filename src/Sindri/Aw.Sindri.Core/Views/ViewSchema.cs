namespace Aw.Sindri.Core.Views;

/// <summary>The view schema version: bump it when a projection's fold changes, and the projector rebuilds.</summary>
public static class ViewSchema
{
    /// <summary>The version this build folds with (3: API P4's request, decision and needs-you views).</summary>
    public const int Version = 3;
}
