namespace Aw.Sindri.Core.Views;

/// <summary>
/// The view schema version: bump it when a projection's fold changes or a view row record's shape does, and the projector rebuilds the views
/// from the ledger on the next start. A test pins the row shapes to this number, so a shape change without a bump fails the build.
/// </summary>
public static class ViewSchema
{
    /// <summary>The version this build folds with (22: the project-attached row carries notes (free text) in place of the one-line note (owner q158); 21: the project-attached and project-hidden views (projects page Ph3); 20: a park row carries its sandbox refusal in the owner's words (SB-6); 19: the launch row carries the sandbox summary from launch.started (sandbox SB-6); 18: the model-history fold keys a mirrored row by the workflow id, so history rows a real run never wrote are now written (#1481); 17: the launch row carries the model from launch.started (#1008); 16: a parked suspension carries its sandbox marker and the launch and self-check park kinds (U9, #1113); 15: the model-history and model-runs views (model-pick M-4); 14: the launch row carries the sandbox failure reason, source and limit ending from launch.failed's detail (sandbox SB-0, spec rev 7); 13: the services row carries pausedBy/pausedAt and lastRun.detail (B-15); 12: the launch row folds launch.takeover-opened (viewer, attach) (#1004); 11: the engine item part carries the unavailable park (#1001); 10: NeedsYouRow.Open became required, and the row shapes are pinned (#959); 9: the code-index view; 8: API P12's recipe stats cache; 7: launch rows carry the instance, Step and attempt; 6: API P10a's launches view; 5: API P11's engine item states; 4: API P6's jobs view and the failure row's new cli; 3: API P4's request, decision and needs-you views).</summary>
    public const int Version = 22;
}
