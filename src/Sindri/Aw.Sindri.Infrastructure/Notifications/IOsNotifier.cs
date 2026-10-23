namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// The operating system's notification centre (engine spec §6.3: "shown as a macOS notification when one is
/// available"; README X5, X50). Only <see cref="FileDeskNotices"/> calls it, for interrupting notices; tests replace it
/// with a fake so no real notification fires.
/// </summary>
internal interface IOsNotifier
{
    /// <summary>Shows a notification, or does nothing where none is available. Never fails the caller.</summary>
    /// <param name="title">The title (the instance address).</param>
    /// <param name="text">The body.</param>
    /// <param name="deepLink">Where a click on the banner goes, as the app's deep link text ('workflow/ITM-01JN2/design/1'); null for none. Only a notifier that can act on a click uses it.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>Done.</returns>
    public ValueTask Show(string title, string text, string? deepLink, CancellationToken cancellationToken);
}
