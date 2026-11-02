namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Take over from the run inspector and the instance sheet.</summary>
internal static partial class Labels
{
    /// <summary>Take over was pressed and the app has no way to send it.</summary>
    internal const string TakeoverNotSent = "Take over wasn't sent: the app has no connection to Sindri. Try again once it is back.";

    /// <summary>A refused takeover: the words the app has for the code, else the message as served, then the code.</summary>
    /// <param name="code">The SND code.</param>
    /// <param name="message">The message as served.</param>
    /// <returns>The sentence.</returns>
    internal static string TakeoverRefused(string code, string message) => $"{AttachRefusal(code) ?? message} ({code})";
}
