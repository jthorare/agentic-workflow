namespace Aw.Sindri.Core.Views;

/// <summary>
/// The view schema version: bump it when a projection's fold changes or a view row record's shape does, and the projector rebuilds the views
/// from the ledger on the next start. A test pins the row shapes to this number, so a shape change without a bump fails the build.
/// </summary>
public static class ViewSchema
{
    /// <summary>The version this build folds with (13: the services row carries pausedBy/pausedAt and lastRun.detail (B-15); 12: the launch row folds launch.takeover-opened (viewer, attach) (#1004); 11: the engine item part carries the unavailable park (#1001); 10: NeedsYouRow.Open became required, and the row shapes are pinned (#959); 9: the code-index view; 8: API P12's recipe stats cache; 7: launch rows carry the instance, Step and attempt; 6: API P10a's launches view; 5: API P11's engine item states; 4: API P6's jobs view and the failure row's new cli; 3: API P4's request, decision and needs-you views).</summary>
    public const int Version = 13;
}
