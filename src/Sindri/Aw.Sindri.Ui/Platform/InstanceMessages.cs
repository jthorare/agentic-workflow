namespace Aw.Sindri.Ui.Platform;

/// <summary>The messages a later launch sends the running instance (<see cref="ISingleInstance.Send"/>).</summary>
public static class InstanceMessages
{
    /// <summary>Bring the window forward; navigate nowhere.</summary>
    public const string Focus = "focus";

    /// <summary>The longest message a running instance reads (a deep link is far shorter); longer ones are dropped.</summary>
    public const int MaxLength = 512;

    /// <summary>Whether a launch's argument is a deep link worth forwarding: a path with a subject, or a page link with none ('projects', 'setup').</summary>
    /// <param name="text">The argument, without the 'sindri://' scheme.</param>
    /// <returns>True when it is a link.</returns>
    public static bool IsLink(string text) =>
        text.Contains('/', StringComparison.Ordinal) || Navigation.DeepLink.TryParse(text, out _);
}
