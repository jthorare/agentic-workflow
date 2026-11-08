using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the toast (todo spec §3.3, §3.5).</summary>
internal static partial class Labels
{
    /// <summary>The toast's region name, for F6 and the screen reader.</summary>
    internal const string ToastRegion = "Notification";

    /// <summary>The Undo button's name for a screen reader.</summary>
    internal const string ToastUndoName = "Undo";

    /// <summary>The plain toast after an Undo that did not work.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>"Couldn't undo: reason".</returns>
    internal static string ToastUndoFailed(string reason) => "Couldn't undo: " + reason;

    /// <summary>The Undo button's words on a toast with no countdown, with the key.</summary>
    /// <param name="macOS">Whether the app runs on macOS.</param>
    /// <returns>"Undo (Ctrl+Z)" or "Undo (⌘Z)".</returns>
    internal static string ToastUndo(bool macOS) => macOS ? "Undo (⌘Z)" : "Undo (Ctrl+Z)";

    /// <summary>The Undo button's words on a toast that counts down.</summary>
    /// <param name="seconds">Seconds left.</param>
    /// <returns>The words, such as "Undo (9 s)".</returns>
    internal static string ToastUndoCountdown(int seconds) => string.Create(CultureInfo.InvariantCulture, $"Undo ({seconds} s)");

    /// <summary>What a screen reader says after a toast's text when it offers Undo: the key, spelled out.</summary>
    /// <param name="macOS">Whether the app runs on macOS.</param>
    /// <returns>The sentence.</returns>
    internal static string ToastSpokenKey(bool macOS) => macOS ? "Undo with Command Z" : "Undo with Control Z";

    /// <summary>The time a counting toast's Undo lasts, said once.</summary>
    /// <param name="seconds">The window's length.</param>
    /// <returns>The words, such as "within 10 seconds".</returns>
    internal static string ToastSpokenWithin(int seconds) => string.Create(CultureInfo.InvariantCulture, $"within {seconds} seconds");
}
